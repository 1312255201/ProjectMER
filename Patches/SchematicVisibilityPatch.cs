using HarmonyLib;
using LabApi.Features.Wrappers;
using Mirror;
using ProjectMER.Features.Objects;

namespace ProjectMER.Patches;

[HarmonyPatch(typeof(CustomInterestManagement), nameof(CustomInterestManagement.OnCheckObserver))]
public static class SchematicVisibilityCheckObserverPatch
{
	public static void Postfix(NetworkIdentity identity, NetworkConnectionToClient newObserver, ref bool __result)
	{
		if (!__result || !TryGetRestrictedSchematic(identity, out SchematicObject schematic))
			return;

		Player? player = GetPlayer(newObserver);
		if (player == null || !schematic.CanBeSeenBy(player))
			__result = false;
	}

	internal static bool TryGetRestrictedSchematic(NetworkIdentity identity, out SchematicObject schematic)
	{
		schematic = null!;
		if (identity == null || SchematicObject.RestrictedVisibilitySchematics.Count == 0)
			return false;

		schematic = identity.GetComponentInParent<SchematicObject>();
		return schematic != null && schematic.VisiblePlayers != null;
	}

	internal static Player? GetPlayer(NetworkConnectionToClient connection)
	{
		if (connection?.identity == null)
			return null;

		return Player.Get(connection.identity);
	}
}

[HarmonyPatch(typeof(CustomInterestManagement), nameof(CustomInterestManagement.OnRebuildObservers))]
public static class SchematicVisibilityRebuildObserversPatch
{
	public static void Postfix(NetworkIdentity identity, HashSet<NetworkConnectionToClient> newObservers)
	{
		if (!SchematicVisibilityCheckObserverPatch.TryGetRestrictedSchematic(identity, out SchematicObject schematic))
			return;

		newObservers.RemoveWhere(connection =>
		{
			Player? player = SchematicVisibilityCheckObserverPatch.GetPlayer(connection);
			return player == null || !schematic.CanBeSeenBy(player);
		});
	}
}
