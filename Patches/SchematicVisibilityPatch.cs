using HarmonyLib;
using Mirror;
using ProjectMER.Features.Objects;

namespace ProjectMER.Patches;

[HarmonyPatch(typeof(CustomInterestManagement), nameof(CustomInterestManagement.OnCheckObserver))]
public static class SchematicVisibilityCheckObserverPatch
{
	public static void Postfix(NetworkIdentity identity, NetworkConnectionToClient newObserver, ref bool __result)
	{
		if (!__result || !TryGet(identity, out SchematicObject schematic))
			return;

		if (!schematic.Allows(newObserver))
			__result = false;
	}

	internal static bool TryGet(NetworkIdentity identity, out SchematicObject schematic)
	{
		schematic = null!;
		if (identity == null || SchematicObject.RestrictedVisibilitySchematics.Count == 0)
			return false;

		try
		{
			schematic = identity.GetComponentInParent<SchematicObject>();
		}
		catch
		{
			return false;
		}

		return schematic != null && schematic.VisiblePlayers != null;
	}
}

[HarmonyPatch(typeof(CustomInterestManagement), nameof(CustomInterestManagement.OnRebuildObservers))]
public static class SchematicVisibilityRebuildObserversPatch
{
	public static bool Prefix(NetworkIdentity identity, HashSet<NetworkConnectionToClient> newObservers)
	{
		if (newObservers == null || !SchematicVisibilityCheckObserverPatch.TryGet(identity, out SchematicObject schematic))
			return true;

		foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
		{
			if (connection != null && connection.isReady && schematic.Allows(connection))
				newObservers.Add(connection);
		}

		NetworkConnectionToClient? localConnection = NetworkServer.localConnection;
		if (localConnection != null && schematic.Allows(localConnection))
			newObservers.Add(localConnection);

		return false;
	}
}
