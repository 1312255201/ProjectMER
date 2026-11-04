using AdminToys;
using System.Reflection;
using LabApi.Features.Wrappers;
using Mirror;
using ProjectMER.Events.Handlers;
using ProjectMER.Features.Enums;
using ProjectMER.Features.Extensions;
using ProjectMER.Features.Serializable.Schematics;
using ProjectMER.Patches;
using UnityEngine;
using Utf8Json;
using Utils.NonAllocLINQ;
using Object = UnityEngine.Object;

namespace ProjectMER.Features.Objects;

public class SchematicObject : MonoBehaviour
{
	/// <summary>
	/// Gets the schematic name.
	/// </summary>
	public string Name { get; private set; }

	/// <summary>
	/// Gets a schematic directory path.
	/// </summary>
	public string DirectoryPath { get; private set; }

	/// <summary>
	/// Gets or sets the global position of the object.
	/// </summary>
	public Vector3 Position
	{
		get => transform.position;
		set
		{
			transform.position = value;
		}
	}

	/// <summary>
	/// Gets or sets the global rotation of the object.
	/// </summary>
	public Quaternion Rotation
	{
		get => transform.rotation;
		set
		{
			transform.rotation = value;
		}
	}

	/// <summary>
	/// Gets or sets the global euler angles of the object.
	/// </summary>
	public Vector3 EulerAngles
	{
		get => Rotation.eulerAngles;
		set => Rotation = Quaternion.Euler(value);
	}

	/// <summary>
	/// Gets or sets the scale of the object.
	/// </summary>
	public Vector3 Scale
	{
		get => transform.localScale;
		set
		{
			transform.localScale = value;
		}
	}

	public IReadOnlyList<GameObject> AttachedBlocks
	{
		get
		{
			if (_attachedBlocks.Count != 0 && _attachedBlocks.All(x => x != null))
				return _attachedBlocks;

			_attachedBlocks.Clear();
			foreach (Transform transform in GetComponentsInChildren<Transform>())
			{
				if (transform == this.transform)
					continue;

				_attachedBlocks.Add(transform.gameObject);
			}

			return _attachedBlocks;
		}
	}

	public IReadOnlyList<NetworkIdentity> NetworkIdentities
	{
		get
		{
			if (_networkIdentities.Count > 0 && _networkIdentities.All(x => x != null))
				return _networkIdentities;

			_networkIdentities.Clear();
			foreach (GameObject block in AttachedBlocks)
			{
				if (block.TryGetComponent(out NetworkIdentity networkIdentity))
					_networkIdentities.Add(networkIdentity);
			}

			return _networkIdentities;
		}
	}

	public IReadOnlyList<AdminToyBase> AdminToyBases
	{
		get
		{
			if (_adminToyBases.Count > 0 && _adminToyBases.All(x => x != null))
				return _adminToyBases;

			_adminToyBases.Clear();
			foreach (NetworkIdentity netId in NetworkIdentities)
			{
				if (netId.TryGetComponent(out AdminToyBase adminToyBase))
					_adminToyBases.Add(adminToyBase);
			}

			return _adminToyBases;
		}
	}

	public AnimationController AnimationController => AnimationController.Get(this);

	/// <summary>
	/// Gets the players that can see this schematic. <see langword="null"/> means everyone can see it.
	/// </summary>
	public IReadOnlyCollection<Player>? VisiblePlayers => _visiblePlayers;

	/// <summary>The player currently being followed, or null if following has stopped.</summary>
	public Player? FollowedPlayer => _followedPlayer;

	/// <summary>Follows a player while preserving the current world position, rotation and scale.</summary>
	public void FollowPlayer(Player player)
	{
		Transform target = GetFollowTarget(player);
		FollowPlayer(player, target.InverseTransformPoint(transform.position), Quaternion.Inverse(target.rotation) * transform.rotation);
	}

	/// <summary>Follows a player at the specified player-local position and rotation, preserving world scale.</summary>
	public void FollowPlayer(Player player, Vector3 localPosition, Quaternion localRotation)
	{
		Transform target = GetFollowTarget(player);
		Vector3 targetScale = target.lossyScale;
		if (Mathf.Approximately(targetScale.x, 0f) || Mathf.Approximately(targetScale.y, 0f) || Mathf.Approximately(targetScale.z, 0f))
			throw new InvalidOperationException("Cannot follow a player with zero scale.");

		NetworkIdentity[] identities = GetComponentsInChildren<NetworkIdentity>(true);
		foreach (NetworkIdentity identity in identities)
		{
			if (identity == null || !identity.TryGetComponent(out AdminToyBase _) ||
				(identity.transform != transform && (identity.transform.parent == null || !identity.transform.parent.TryGetComponent(out NetworkIdentity _))))
				throw new NotSupportedException("Following requires admin toy blocks with networked parents; pickups and workstations are not supported.");
		}
		if (!TryGetComponent(out AdminToyBase rootToy))
			throw new NotSupportedException("The schematic root must be an admin toy.");

		Vector3 worldScale = transform.lossyScale;
		transform.SetParent(null, true);
		_followScale = new Vector3(worldScale.x / targetScale.x, worldScale.y / targetScale.y, worldScale.z / targetScale.z);
		_followLocalPosition = localPosition;
		_followLocalRotation = localRotation;
		_followedPlayer = player;
		_followTarget = target;
		rootToy.NetworkIsStatic = false;
		UpdateFollowTransform();
	}

	/// <summary>Stops following and leaves the schematic at its current world transform.</summary>
	public void StopFollowing()
	{
		_followedPlayer = null;
		_followTarget = null;
	}

	private static Transform GetFollowTarget(Player player)
	{
		if (player == null)
			throw new ArgumentNullException(nameof(player));
		if (!player.IsOnline || player.GameObject == null)
			throw new InvalidOperationException("The player must be online to follow them.");
		return player.GameObject.transform;
	}

	private void LateUpdate()
	{
		if (_followedPlayer == null)
			return;
		if (!_followedPlayer.IsOnline || _followTarget == null)
		{
			StopFollowing();
			return;
		}
		UpdateFollowTransform();
	}

	private void UpdateFollowTransform()
	{
		if (_followTarget == null)
			return;
		transform.SetPositionAndRotation(_followTarget.TransformPoint(_followLocalPosition), _followTarget.rotation * _followLocalRotation);
		transform.localScale = Vector3.Scale(_followTarget.lossyScale, _followScale);
		if (TryGetComponent(out AdminToyBase rootToy))
		{
			rootToy.NetworkPosition = transform.localPosition;
			rootToy.NetworkRotation = transform.localRotation;
			rootToy.NetworkScale = transform.localScale;
		}
	}

	public SchematicObject Init(SchematicObjectDataList data)
	{
		Name = Path.GetFileNameWithoutExtension(data.Path);
		DirectoryPath = data.Path;

		ObjectFromId = new Dictionary<int, Transform>(data.Blocks.Count + 1)
		{
			{ data.RootObjectId, transform },
		};

		CreateRecursiveFromID(data.RootObjectId, data.Blocks, transform);

		AddRigidbodies();
		AddAnimators();
		HideFromDisallowedPlayers();

		Schematic.OnSchematicSpawned(new(this, Name));

		return this;
	}

	private void CreateRecursiveFromID(int id, List<SchematicBlockData> blocks, Transform parentGameObject)
	{
		Transform childGameObjectTransform = CreateObject(blocks.Find(c => c.ObjectId == id), parentGameObject) ?? transform; // Create the object first before creating children.
		int[] parentSchematics = blocks.Where(bl => bl.BlockType == BlockType.Schematic).Select(bl => bl.ObjectId).ToArray();

		// Gets all the ObjectIds of all the schematic blocks inside "blocks" argument.
		foreach (SchematicBlockData block in blocks.FindAll(c => c.ParentId == id))
		{
			if (parentSchematics.Contains(block.ParentId)) // The block is a child of some schematic inside "parentSchematics" array, therefore it will be skipped to avoid spawning it and its children twice.
				continue;

			CreateRecursiveFromID(block.ObjectId, blocks, childGameObjectTransform); // The child now becomes the parent
		}
	}

	private Transform? CreateObject(SchematicBlockData block, Transform parentTransform)
	{
		if (block == null)
			return null;

		GameObject gameObject = block.Create(this, parentTransform);
		NetworkServer.Spawn(gameObject);

		ObjectFromId.Add(block.ObjectId, gameObject.transform);

		if (block.BlockType != BlockType.Light && TryGetAnimatorController(block.AnimatorName, out RuntimeAnimatorController animatorController))
			_animators.Add(gameObject, animatorController);

		return gameObject.transform;
	}

	private bool TryGetAnimatorController(string animatorName, out RuntimeAnimatorController animatorController)
	{
		animatorController = null!;

		if (string.IsNullOrEmpty(animatorName))
			return false;

		Object? animatorObject = AssetBundle.GetAllLoadedAssetBundles().FirstOrDefault(x => x.mainAsset.name == animatorName)?.LoadAllAssets().First(x => x is RuntimeAnimatorController);

		if (animatorObject is null)
		{
			string path = Path.Combine(DirectoryPath, animatorName);

			if (!File.Exists(path))
			{
				Logger.Warn($"{gameObject.name} block of schematic should have a {animatorName} animator attached, but the file does not exist!");
				return false;
			}

			animatorObject = AssetBundle.LoadFromFile(path).LoadAllAssets().First(x => x is RuntimeAnimatorController);
		}

		animatorController = (RuntimeAnimatorController)animatorObject;
		return true;
	}

	private bool AddAnimators()
	{
		bool isAnimated = false;
		if (!_animators.IsEmpty())
		{
			isAnimated = true;
			foreach (KeyValuePair<GameObject, RuntimeAnimatorController> pair in _animators)
			{
				Animator animator = pair.Key.AddComponent<Animator>();
				animator.runtimeAnimatorController = pair.Value;
				animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
				if (pair.Key.TryGetComponent(out AdminToyBase adminToyBase))
					adminToyBase.NetworkIsStatic = false;
			}
		}

		_animators.Clear();
		AssetBundle.UnloadAllAssetBundles(false);
		return isAnimated;
	}

	private bool AddRigidbodies()
	{
		bool hasRigidbodies = false;
		string rigidbodyPath = Path.Combine(DirectoryPath, $"{Name}-Rigidbodies.json");
		if (!File.Exists(rigidbodyPath))
			return false;

		foreach (KeyValuePair<int, SerializableRigidbody> dict in JsonSerializer.Deserialize<Dictionary<int, SerializableRigidbody>>(File.ReadAllText(rigidbodyPath)))
		{
			if (!ObjectFromId.TryGetValue(dict.Key, out Transform transform))
				continue;

			if (!transform.gameObject.TryGetComponent(out Rigidbody rigidbody))
				rigidbody = transform.gameObject.AddComponent<Rigidbody>();

			rigidbody.isKinematic = dict.Value.IsKinematic;
			rigidbody.useGravity = dict.Value.UseGravity;
			rigidbody.constraints = dict.Value.Constraints;
			rigidbody.mass = dict.Value.Mass;

			hasRigidbodies = true;
		}

		return hasRigidbodies;
	}


	/// <summary>Destroys the schematic and all of its independently spawned network objects.</summary>
	public void Destroy()
	{
		if (_destroying)
			return;

		_destroying = true;
		DestroyNetworkChildren();
		NetworkServer.Destroy(gameObject);
	}

	/// <summary>Replaces the visibility list; null makes the schematic visible to everyone.</summary>
	public void SetVisiblePlayers(IEnumerable<Player>? players)
	{
		HashSet<Player>? updated = players == null ? null : new HashSet<Player>(players.Where(player => player != null));
		_visiblePlayers = updated;
		if (updated == null)
			RestrictedVisibilitySchematics.Remove(this);
		else
			RestrictedVisibilitySchematics.Add(this);
		RebuildVisibility();
	}

	/// <summary>Adds a player to a restricted visibility list. Returns false if already present.</summary>
	public bool AddVisiblePlayer(Player player)
	{
		if (player == null)
			throw new ArgumentNullException(nameof(player));
		if (_visiblePlayers == null)
			throw new InvalidOperationException("Set a restricted visibility list before adding players.");
		if (!_visiblePlayers.Add(player))
			return false;
		RebuildVisibility();
		return true;
	}

	/// <summary>Removes a player from a restricted visibility list. Returns false if absent.</summary>
	public bool RemoveVisiblePlayer(Player player)
	{
		if (player == null)
			throw new ArgumentNullException(nameof(player));
		if (_visiblePlayers == null)
			throw new InvalidOperationException("Set a restricted visibility list before removing players.");
		if (!_visiblePlayers.Remove(player))
			return false;
		RebuildVisibility();
		return true;
	}

	private void RebuildVisibility()
	{
		NetworkIdentity[] identities = GetComponentsInChildren<NetworkIdentity>(true);
		// Hide children before their parents, and spawn parents before children.
		for (int i = identities.Length - 1; i >= 0; i--)
		{
			NetworkIdentity identity = identities[i];
			if (identity != null && identity.netId != 0)
				SchematicRebuildObserversPatch.Refresh(identity, this, add: false);
		}
		foreach (NetworkIdentity identity in identities)
		{
			if (identity != null && identity.netId != 0)
				SchematicRebuildObserversPatch.Refresh(identity, this, remove: false);
		}
	}

	private void DestroyNetworkChildren()
	{
		NetworkIdentity[] identities = GetComponentsInChildren<NetworkIdentity>(true);
		for (int i = identities.Length - 1; i >= 0; i--)
		{
			NetworkIdentity identity = identities[i];
			if (identity != null && identity.gameObject != gameObject && identity.netId != 0)
				NetworkServer.Destroy(identity.gameObject);
		}
	}

	internal static HashSet<Player>? PendingVisiblePlayers;

	internal void AssignVisiblePlayers(IEnumerable<Player>? visiblePlayers)
	{
		if (visiblePlayers == null || _visiblePlayers != null)
			return;

		_visiblePlayers = [];
		foreach (Player player in visiblePlayers)
		{
			if (player != null)
				_visiblePlayers.Add(player);
		}

		RestrictedVisibilitySchematics.Add(this);
	}

	internal bool Allows(NetworkConnection connection)
	{
		if (_visiblePlayers == null)
			return true;

		if (connection == null)
			return false;

		int connectionId = connection.connectionId;
		foreach (Player allowed in _visiblePlayers)
		{
			if (allowed == null)
				continue;

			if (allowed.ConnectionToClient != null && allowed.ConnectionToClient.connectionId == connectionId)
				return true;

			if (allowed.Connection != null && allowed.Connection.connectionId == connectionId)
				return true;
		}

		return false;
	}

	internal void HideFromDisallowedPlayers()
	{
		if (_visiblePlayers == null)
			return;

		foreach (NetworkIdentity identity in GetComponentsInChildren<NetworkIdentity>(true))
		{
			if (identity == null || identity.netId == 0 || identity.observers == null || identity.observers.Count == 0)
				continue;

			List<NetworkConnectionToClient> denied = [];
			foreach (NetworkConnectionToClient connection in identity.observers.Values)
			{
				if (connection != null && !Allows(connection))
					denied.Add(connection);
			}

			if (denied.Count == 0)
				continue;

			foreach (NetworkConnectionToClient connection in denied)
			{
				RemoveFromObserving?.Invoke(connection, [identity, false]);
				identity.observers.Remove(connection.connectionId);
			}
		}
	}

	private void OnDestroy()
	{
		if (!_destroying)
		{
			_destroying = true;
			DestroyNetworkChildren();
			NetworkServer.Destroy(gameObject);
		}
		RestrictedVisibilitySchematics.Remove(this);
		AnimationController.Dictionary.Remove(this);
		Schematic.OnSchematicDestroyed(new(this, Name));
	}

	internal static readonly HashSet<SchematicObject> RestrictedVisibilitySchematics = [];

	private static readonly MethodInfo? RemoveFromObserving = typeof(NetworkConnectionToClient).GetMethod(
		"RemoveFromObserving",
		BindingFlags.Instance | BindingFlags.NonPublic,
		null,
		[typeof(NetworkIdentity), typeof(bool)],
		null);

	internal Dictionary<int, Transform> ObjectFromId = [];

	private HashSet<Player>? _visiblePlayers;
	private Player? _followedPlayer;
	private Transform? _followTarget;
	private Vector3 _followLocalPosition;
	private Quaternion _followLocalRotation;
	private Vector3 _followScale;
	private bool _destroying;
	private readonly List<GameObject> _attachedBlocks = [];
	private readonly List<NetworkIdentity> _networkIdentities = [];
	private readonly List<AdminToyBase> _adminToyBases = [];
	private readonly Dictionary<GameObject, RuntimeAnimatorController> _animators = [];
}
