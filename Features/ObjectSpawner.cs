using AdminToys;
using LabApi.Features.Wrappers;
using ProjectMER.Features.Objects;
using ProjectMER.Features.Serializable;
using ProjectMER.Features.Serializable.Schematics;
using UnityEngine;
using PrimitiveObjectToy = AdminToys.PrimitiveObjectToy;

namespace ProjectMER.Features;

public static class ObjectSpawner
{
	public static PrimitiveObjectToy SpawnPrimitive(SerializablePrimitive serializablePrimitive)
	{
		GameObject gameObject = serializablePrimitive.SpawnOrUpdateObject();
		return gameObject.GetComponent<PrimitiveObjectToy>();
	}

	public static SchematicObject SpawnSchematic(SerializableSchematic serializableSchematic, IEnumerable<Player>? visiblePlayers = null)
	{
		HashSet<Player>? pending = null;
		if (visiblePlayers != null)
		{
			pending = [];
			foreach (Player player in visiblePlayers)
			{
				if (player != null)
					pending.Add(player);
			}
		}

		SchematicObject.PendingVisiblePlayers = pending;
		try
		{
			GameObject? gameObject = serializableSchematic.SpawnOrUpdateObject();
			if (gameObject == null)
				return null!;

			return gameObject.GetComponent<SchematicObject>();
		}
		finally
		{
			SchematicObject.PendingVisiblePlayers = null;
		}
	}

	public static SchematicObject SpawnSchematic(SerializableSchematic serializableSchematic, Player? visiblePlayer) =>
		SpawnSchematic(serializableSchematic, visiblePlayer == null ? null : [visiblePlayer]);

	public static SchematicObject SpawnSchematic(string schematicName, Vector3 position, IEnumerable<Player>? visiblePlayers = null) =>
		SpawnSchematic(new() { SchematicName = schematicName, Position = position }, visiblePlayers);

	public static SchematicObject SpawnSchematic(string schematicName, Vector3 position, Player? visiblePlayer) =>
		SpawnSchematic(schematicName, position, visiblePlayer == null ? null : [visiblePlayer]);

	public static SchematicObject SpawnSchematic(string schematicName, Vector3 position, Quaternion rotation, IEnumerable<Player>? visiblePlayers = null) =>
		SpawnSchematic(new() { SchematicName = schematicName, Position = position, Rotation = rotation.eulerAngles }, visiblePlayers);

	public static SchematicObject SpawnSchematic(string schematicName, Vector3 position, Quaternion rotation, Player? visiblePlayer) =>
		SpawnSchematic(schematicName, position, rotation, visiblePlayer == null ? null : [visiblePlayer]);

	public static SchematicObject SpawnSchematic(string schematicName, Vector3 position, Vector3 eulerAngles, IEnumerable<Player>? visiblePlayers = null) =>
		SpawnSchematic(new() { SchematicName = schematicName, Position = position, Rotation = eulerAngles }, visiblePlayers);

	public static SchematicObject SpawnSchematic(string schematicName, Vector3 position, Vector3 eulerAngles, Player? visiblePlayer) =>
		SpawnSchematic(schematicName, position, eulerAngles, visiblePlayer == null ? null : [visiblePlayer]);

	public static SchematicObject SpawnSchematic(string schematicName, Vector3 position, Quaternion rotation, Vector3 scale, IEnumerable<Player>? visiblePlayers = null) =>
		SpawnSchematic(new() { SchematicName = schematicName, Position = position, Rotation = rotation.eulerAngles, Scale = scale }, visiblePlayers);

	public static SchematicObject SpawnSchematic(string schematicName, Vector3 position, Quaternion rotation, Vector3 scale, Player? visiblePlayer) =>
		SpawnSchematic(schematicName, position, rotation, scale, visiblePlayer == null ? null : [visiblePlayer]);

	public static SchematicObject SpawnSchematic(string schematicName, Vector3 position, Vector3 eulerAngles, Vector3 scale, IEnumerable<Player>? visiblePlayers = null) =>
		SpawnSchematic(new() { SchematicName = schematicName, Position = position, Rotation = eulerAngles, Scale = scale }, visiblePlayers);

	public static SchematicObject SpawnSchematic(string schematicName, Vector3 position, Vector3 eulerAngles, Vector3 scale, Player? visiblePlayer) =>
		SpawnSchematic(schematicName, position, eulerAngles, scale, visiblePlayer == null ? null : [visiblePlayer]);

	public static bool TrySpawnSchematic(SerializableSchematic serializableSchematic, out SchematicObject schematic, IEnumerable<Player>? visiblePlayers = null)
	{
		try
		{
			schematic = SpawnSchematic(serializableSchematic, visiblePlayers);
			return schematic != null;
		}
		catch (Exception)
		{
			schematic = null!;
			return false;
		}
	}

	public static bool TrySpawnSchematic(SerializableSchematic serializableSchematic, Player? visiblePlayer, out SchematicObject schematic) =>
		TrySpawnSchematic(serializableSchematic, out schematic, visiblePlayer == null ? null : [visiblePlayer]);

	public static bool TrySpawnSchematic(string schematicName, Vector3 position, out SchematicObject schematic, IEnumerable<Player>? visiblePlayers = null) =>
		TrySpawnSchematic(new() { SchematicName = schematicName, Position = position }, out schematic, visiblePlayers);

	public static bool TrySpawnSchematic(string schematicName, Vector3 position, Player? visiblePlayer, out SchematicObject schematic) =>
		TrySpawnSchematic(schematicName, position, out schematic, visiblePlayer == null ? null : [visiblePlayer]);

	public static bool TrySpawnSchematic(string schematicName, Vector3 position, Quaternion rotation, out SchematicObject schematic, IEnumerable<Player>? visiblePlayers = null) =>
		TrySpawnSchematic(new() { SchematicName = schematicName, Position = position, Rotation = rotation.eulerAngles }, out schematic, visiblePlayers);

	public static bool TrySpawnSchematic(string schematicName, Vector3 position, Quaternion rotation, Player? visiblePlayer, out SchematicObject schematic) =>
		TrySpawnSchematic(schematicName, position, rotation, out schematic, visiblePlayer == null ? null : [visiblePlayer]);

	public static bool TrySpawnSchematic(string schematicName, Vector3 position, Vector3 eulerAngles, out SchematicObject schematic, IEnumerable<Player>? visiblePlayers = null) =>
		TrySpawnSchematic(new() { SchematicName = schematicName, Position = position, Rotation = eulerAngles }, out schematic, visiblePlayers);

	public static bool TrySpawnSchematic(string schematicName, Vector3 position, Vector3 eulerAngles, Player? visiblePlayer, out SchematicObject schematic) =>
		TrySpawnSchematic(schematicName, position, eulerAngles, out schematic, visiblePlayer == null ? null : [visiblePlayer]);

	public static bool TrySpawnSchematic(string schematicName, Vector3 position, Quaternion rotation, Vector3 scale, out SchematicObject schematic, IEnumerable<Player>? visiblePlayers = null) =>
		TrySpawnSchematic(new() { SchematicName = schematicName, Position = position, Rotation = rotation.eulerAngles, Scale = scale }, out schematic, visiblePlayers);

	public static bool TrySpawnSchematic(string schematicName, Vector3 position, Quaternion rotation, Vector3 scale, Player? visiblePlayer, out SchematicObject schematic) =>
		TrySpawnSchematic(schematicName, position, rotation, scale, out schematic, visiblePlayer == null ? null : [visiblePlayer]);

	public static bool TrySpawnSchematic(string schematicName, Vector3 position, Vector3 eulerAngles, Vector3 scale, out SchematicObject schematic, IEnumerable<Player>? visiblePlayers = null) =>
		TrySpawnSchematic(new() { SchematicName = schematicName, Position = position, Rotation = eulerAngles, Scale = scale }, out schematic, visiblePlayers);

	public static bool TrySpawnSchematic(string schematicName, Vector3 position, Vector3 eulerAngles, Vector3 scale, Player? visiblePlayer, out SchematicObject schematic) =>
		TrySpawnSchematic(schematicName, position, eulerAngles, scale, out schematic, visiblePlayer == null ? null : [visiblePlayer]);
}
