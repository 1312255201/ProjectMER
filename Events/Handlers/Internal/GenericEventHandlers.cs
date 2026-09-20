using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.CustomHandlers;
using LabApi.Features.Wrappers;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Extensions;
using ProjectMER.Features.Objects;
using ProjectMER.Features.Serializable;
using ProjectMER.Features.ToolGun;

namespace ProjectMER.Events.Handlers.Internal;

public class GenericEventsHandler : CustomEventsHandler
{
	public override void OnServerWaitingForPlayers()
	{
		PrefabManager.RegisterPrefabs();

		MapUtils.LoadedMaps.Clear();
		ToolGunItem.ItemDictionary.Clear();
		ToolGunHandler.PlayerSelectedObjectDict.Clear();
		PickupEventsHandler.ButtonPickups.Clear();
		PickupEventsHandler.PickupUsesLeft.Clear();
		SchematicObject.RestrictedVisibilitySchematics.Clear();
	}

	public override void OnPlayerJoined(PlayerJoinedEventArgs ev)
	{
		Timing.CallDelayed(0.5f, () =>
		{
			Player player = ev.Player;
			if (player == null || !player.IsOnline || player.Connection == null)
				return;

			foreach (SchematicObject schematic in SchematicObject.RestrictedVisibilitySchematics.ToArray())
			{
				if (schematic == null)
					continue;

				if (schematic.CanBeSeenBy(player))
					player.SpawnSchematic(schematic);
			}
		});
	}

	public override void OnPlayerSpawning(PlayerSpawningEventArgs ev)
	{
		if (!ev.UseSpawnPoint)
			return;

		List<MapEditorObject> list = [];
		foreach (MapSchematic map in MapUtils.LoadedMaps.Values)
		{
			foreach (KeyValuePair<string, SerializablePlayerSpawnpoint> spawnpoint in map.PlayerSpawnpoints)
			{
				if (!spawnpoint.Value.Roles.Contains(ev.Role.RoleTypeId))
					continue;

				list.AddRange(map.SpawnedObjects.Where(x => x.Id == spawnpoint.Key));
			}
		}

		if (list.Count == 0)
			return;

		MapEditorObject randomElement = list[UnityEngine.Random.Range(0, list.Count)];

		ev.SpawnLocation = randomElement.transform.position;
		Timing.CallDelayed(0.05f, () =>
		{
			try
			{
				ev.Player.LookRotation = randomElement.transform.eulerAngles;
			}
			catch (Exception e)
			{
				Logger.Error(e);
			}
		});
	}

	public override void OnPlayerInteractingShootingTarget(PlayerInteractingShootingTargetEventArgs ev)
	{
		if (ev.ShootingTarget.GameObject.TryGetComponent(out MapEditorObject _))
			ev.IsAllowed = false;
	}
}
