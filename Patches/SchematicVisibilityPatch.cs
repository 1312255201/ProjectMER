using HarmonyLib;
using Mirror;
using ProjectMER.Features.Objects;
using System.Reflection;

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

// Mirror's default interest management does not rebuild existing observers.
// Maintain both sides of Mirror's observer relationship for restricted models.
[HarmonyPatch(typeof(NetworkServer), nameof(NetworkServer.RebuildObservers))]
public static class SchematicRebuildObserversPatch
{
	private static readonly MethodInfo AddObserver = AccessTools.Method(typeof(NetworkIdentity), "AddObserver", [typeof(NetworkConnectionToClient)]);
	private static readonly MethodInfo RemoveFromObserving = AccessTools.Method(typeof(NetworkConnectionToClient), "RemoveFromObserving", [typeof(NetworkIdentity), typeof(bool)]);

	public static bool Prefix(NetworkIdentity identity)
	{
		if (!SchematicVisibilityCheckObserverPatch.TryGet(identity, out SchematicObject schematic))
			return true;
		Refresh(identity, schematic);
		return false;
	}

	internal static void Refresh(NetworkIdentity identity, SchematicObject schematic, bool remove = true, bool add = true)
	{
		if (identity == null || identity.netId == 0 || identity.observers == null)
			return;

		HashSet<NetworkConnectionToClient> desired = [];
		foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
		{
			if (connection != null && connection.isReady && schematic.Allows(connection))
				desired.Add(connection);
		}
		NetworkConnectionToClient? local = NetworkServer.localConnection;
		if (local != null && local.isReady && schematic.Allows(local))
			desired.Add(local);

		if (remove)
		{
			foreach (NetworkConnectionToClient connection in identity.observers.Values.ToArray())
			{
				if (connection != null && !desired.Contains(connection))
				{
					RemoveFromObserving.Invoke(connection, [identity, false]);
					identity.observers.Remove(connection.connectionId);
				}
			}
		}
		if (add)
		{
			foreach (NetworkConnectionToClient connection in desired)
			{
				if (!identity.observers.ContainsKey(connection.connectionId))
					AddObserver.Invoke(identity, [connection]);
			}
		}
	}
}

// Also guard Mirror's initial observer setup when no interest manager is active.
[HarmonyPatch(typeof(NetworkIdentity), "AddObserver")]
public static class SchematicAddObserverPatch
{
	public static bool Prefix(NetworkIdentity __instance, NetworkConnectionToClient conn) =>
		!SchematicVisibilityCheckObserverPatch.TryGet(__instance, out SchematicObject schematic) || schematic.Allows(conn);
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
