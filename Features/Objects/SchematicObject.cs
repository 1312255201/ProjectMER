using AdminToys;
using System.Reflection;
using LabApi.Features.Wrappers;
using Mirror;
using ProjectMER.Events.Handlers;
using ProjectMER.Features.Enums;
using ProjectMER.Features.Extensions;
using ProjectMER.Features.Serializable.Schematics;
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


	public void Destroy() => Destroy(gameObject);

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
		RestrictedVisibilitySchematics.Remove(this);
		AnimationController.Dictionary.Remove(this);
		NetworkServer.Destroy(gameObject);
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
	private readonly List<GameObject> _attachedBlocks = [];
	private readonly List<NetworkIdentity> _networkIdentities = [];
	private readonly List<AdminToyBase> _adminToyBases = [];
	private readonly Dictionary<GameObject, RuntimeAnimatorController> _animators = [];
}
