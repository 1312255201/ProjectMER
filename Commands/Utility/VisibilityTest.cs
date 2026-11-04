using CommandSystem;
using LabApi.Features.Wrappers;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using UnityEngine;

namespace ProjectMER.Commands.Utility;

/// <summary>Commands for exercising schematic visibility and destruction in game.</summary>
public class VisibilityTest : ICommand
{
	private static readonly Dictionary<int, SchematicObject> Models = [];
	private static int _nextId = 1;

	public string Command => "vistest";
	public string[] Aliases => [];
	public string Description => "Test schematic visibility and following: spawn, add, remove, follow, stop, list, destroy.";

	public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
	{
		if (!CommandAccess.IsRemoteAdmin(sender))
		{
			response = "Remote Admin access is required.";
			return false;
		}

		if (arguments.Count == 0)
			return Usage(out response);

		string action = arguments.At(0).ToLowerInvariant();
		if (action == "list" && arguments.Count == 1)
		{
			RemoveDestroyed();
			response = Models.Count == 0 ? "No test schematics." : string.Join("\n", Models.Select(pair =>
				$"#{pair.Key}: {pair.Value.Name}; visible player IDs: {string.Join(", ", pair.Value.VisiblePlayers?.Where(p => p != null).Select(p => p.PlayerId) ?? Enumerable.Empty<int>())}"));
			return true;
		}

		if (action == "spawn" && arguments.Count == 3)
		{
			Player? executor = Player.Get(sender);
			if (executor == null)
			{
				response = "Spawn must be run by a player; the schematic appears two units from their position.";
				return false;
			}
			if (!TryPlayer(arguments.At(2), out Player target, out response))
				return false;

			try
			{
				Vector3 position = executor.Position + Vector3.forward * 2f;
				SchematicObject model = ObjectSpawner.SpawnSchematic(arguments.At(1), position, new[] { target });
				if (model == null)
				{
					response = "Could not spawn the schematic. Check its name and server logs.";
					return false;
				}
				int id = _nextId++;
				Models.Add(id, model);
				response = $"Spawned test schematic #{id} at {position}; initially visible to player {target.PlayerId}.";
				return true;
			}
			catch (Exception exception)
			{
				response = $"Could not spawn schematic: {exception.Message}";
				return false;
			}
		}

		if ((action == "add" || action == "remove" || action == "follow" || action == "stop" || action == "destroy") &&
			arguments.Count == (action == "destroy" || action == "stop" ? 2 : 3))
		{
			RemoveDestroyed();
			if (!int.TryParse(arguments.At(1), out int id) || !Models.TryGetValue(id, out SchematicObject model))
			{
				response = "Unknown test schematic ID. Use mp vistest list.";
				return false;
			}
			if (action == "destroy")
			{
				model.Destroy();
				Models.Remove(id);
				response = $"Destroyed test schematic #{id}.";
				return true;
			}
			if (action == "stop")
			{
				model.StopFollowing();
				response = $"Schematic #{id} stopped following.";
				return true;
			}
			if (!TryPlayer(arguments.At(2), out Player target, out response))
				return false;
			if (action == "follow")
			{
				try
				{
					model.FollowPlayer(target);
					response = $"Schematic #{id} now follows player {target.PlayerId}, preserving its current position.";
					return true;
				}
				catch (Exception exception)
				{
					response = $"Could not follow player: {exception.Message}";
					return false;
				}
			}
			bool changed = action == "add" ? model.AddVisiblePlayer(target) : model.RemoveVisiblePlayer(target);
			response = changed ? $"Updated schematic #{id} for player {target.PlayerId}." : "Visibility list was already in the requested state.";
			return true;
		}

		return Usage(out response);
	}

	private static bool TryPlayer(string value, out Player player, out string response)
	{
		player = (int.TryParse(value, out int id) ? Player.Get(id) : null)!;
		if (player == null || !player.IsOnline)
		{
			response = "Player not found. Supply an online numeric player ID.";
			return false;
		}
		response = string.Empty;
		return true;
	}

	private static void RemoveDestroyed()
	{
		foreach (int id in Models.Where(pair => pair.Value == null).Select(pair => pair.Key).ToArray())
			Models.Remove(id);
	}

	internal static void Clear()
	{
		foreach (SchematicObject model in Models.Values)
		{
			if (model != null)
				model.Destroy();
		}
		Models.Clear();
	}

	private static bool Usage(out string response)
	{
		response = "Usage: mp vistest spawn <schematicName> <playerId> | add/remove/follow <modelId> <playerId> | stop/destroy <modelId> | list";
		return false;
	}
}
