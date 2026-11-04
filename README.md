# Note: This is WIP project of MapEditorReborn for LabAPI. Join discord for more info.

[![MapEditorReborn](https://i.imgur.com/CeemJnt.png)](https://discord.gg/JwAfeSd79u)

<h1 align="center">MapEditorReborn (LabAPI edition)</h1>
<h3 align="center"><a href="https://store.steampowered.com/app/700330/SCP_Secret_Laboratory/">SCP: Secret Laboratory</a> plugin allowing to spawn and modify various objects.</h3>
<div align="center">
    
<img src="https://img.shields.io/github/downloads/Michal78900/MapEditorReborn/total?style=for-the-badge&logo=github" alt="Downloads">
<a href="https://discord.gg/JwAfeSd79u">
    <img src="https://img.shields.io/discord/947849283514814486?style=for-the-badge&logo=discord" alt="Chat on Discord">
</a>    

</div>

# Installation
Put your [`MapEditorReborn.dll`](https://github.com/Michal78900/ProjectMER/releases/latest) file in `LabAPI/plugins/global/`.

Unzip [`Harmony-Fat.2.3.6.0.zip`](https://github.com/pardeike/Harmony/releases/download/v2.3.6.0/Harmony-Fat.2.3.6.0.zip).

Within the unzipped archive, move `net48/0Harmony.dll` to `LabAPI/dependencies/global/`.

Once your plugin loads, it will create a directory at `LabAPI/configs/ProjectMER`. This directory will contain two sub-directories: **Schematics** and **Maps**.

**[Wiki (WIP)](https://github.com/Michal78900/ProjectMER/wiki)**

**[Full MER tutorial](https://docs.google.com/document/d/10V2PnqobeBFb2xTFIHSGmM2KK9_h2wethiVQdcjyhGc/edit?usp=sharing)**

**More support can be found on a [Discord](https://discord.gg/JwAfeSd79u) server**

# Schematic visibility API

Use the `SchematicObject` returned by `ObjectSpawner` to manage visibility and
the schematic's lifetime on the server's main thread:

```csharp
using LabApi.Features.Wrappers;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using UnityEngine;

// An empty array creates a schematic that nobody can see initially.
SchematicObject schematic = ObjectSpawner.SpawnSchematic(
    "MySchematic", Vector3.zero, new[] { firstPlayer });

schematic.AddVisiblePlayer(secondPlayer);    // Show it to secondPlayer.
schematic.RemoveVisiblePlayer(firstPlayer);  // Hide it from firstPlayer.
schematic.SetVisiblePlayers(new[] { firstPlayer, secondPlayer }); // Replace the list.
schematic.SetVisiblePlayers(null);          // Everyone, including future players.

// When finished, destroy the complete schematic on the server.
schematic.Destroy();
```

`AddVisiblePlayer` and `RemoveVisiblePlayer` return whether the list changed.
They require a restricted list and throw `InvalidOperationException` when the
schematic is visible to everyone. Call `SetVisiblePlayers(...)` first to switch
to a restricted list. An empty list means nobody can see the schematic; `null`
means unrestricted visibility. Entries are player instances for the current
connection: add the new player instance after a reconnect if access should persist.
Visibility controls network rendering, not server-side collision or interactions.
Use these methods instead of sending `SpawnSchematic` / `DestroySchematic`
messages directly, which do not change the observer list. Call `Destroy()` on
the `SchematicObject`, rather than destroying individual blocks.

### Following a player

```csharp
// Preserve the current position and rotation as offsets relative to the player.
schematic.FollowPlayer(player);

// Or specify the position and rotation relative to the player's transform.
schematic.FollowPlayer(player, new Vector3(0f, 1f, 0f), Quaternion.identity);

schematic.StopFollowing(); // Remain at the current position.
```

Following does not change the visibility list or parent the schematic to the
player. It stops if the player disconnects and keeps the schematic at its last
position. The model is detached from any previous parent when following starts.
The model root uses dynamic network movement while following and remains dynamic
afterwards. Only schematics composed of admin toy network objects with networked
parents are supported; models containing pickups or workstations cannot follow.
Unsupported models throw `NotSupportedException`. Run these APIs on the server
main thread. Use the player's body transform, not their camera/look rotation.

## In-game visibility test commands

Run these in Remote Admin. Spawn must be run by an in-game player; it creates the
schematic two units along the world Z axis from the executor. Player IDs are the
online numeric IDs; the returned model ID identifies a test schematic.

```text
mp vistest spawn <schematicName> <playerId>
mp vistest add <modelId> <playerId>
mp vistest remove <modelId> <playerId>
mp vistest follow <modelId> <playerId>
mp vistest stop <modelId>
mp vistest list
mp vistest destroy <modelId>
```

The spawn command initially allows only the specified player. `follow` preserves
the model's current position and rotation; `stop` ends following. The player being
followed does not automatically gain visibility. Test schematics are also
destroyed when the server returns to waiting for players or the plugin is
disabled. Commands require the sender's LabAPI player to have `RemoteAdminAccess`;
they do not check individual `mpr.*` permissions. Senders that cannot be resolved
to a player with Remote Admin access are rejected.

# Credits
- Plugin made by [Michal78900](https://github.com/Michal78900)
- Original plugin idea and code overhaul by [Killers0992](https://github.com/Killers0992)
- Another code overhaul and documentation by [Nao](https://github.com/NaoUnderscore)
- Testing the plugin by Cegła, The Jukers server staff and others
