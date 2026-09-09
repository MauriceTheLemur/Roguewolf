# Roguewolf networking foundation

Netcode for GameObjects 2.13.1, **client-server with a server-authoritative host**.

## Why not Distributed Authority

NGO 2.x offers a Distributed Authority mode where clients own their objects. It is the wrong
model for a hidden-role game: `NetworkVariableBase.CanClientRead` returns `true` for everyone
when `DistributedAuthorityMode` is set, so owner-only read permission silently stops working.
Keep `NetworkConfig` in client-server mode.

## The one rule

**Secrets live in owner-read NetworkVariables or targeted RPCs. Never in a public variable
that the UI merely declines to draw.**

NGO checks read permission on the *send* path (`NetworkVariableDeltaMessage` and the spawn-time
full sync), so a variable declared as:

```csharp
new NetworkVariable<RoleId>(RoleId.None,
    NetworkVariableReadPermission.Owner,
    NetworkVariableWritePermission.Server);
```

is never serialized toward non-owners. Those clients cannot read it out of memory or sniff it
off the wire, because they were never sent it.

## Layout

| File | Role |
|---|---|
| `ConnectionData.cs` | Join payload, disconnect reasons, persistent local player GUID |
| `ConnectionManager.cs` | Start/stop session, connection approval, kick |
| `SessionManager.cs` | Server-only GUID → seat map; survives disconnects |
| `NetworkPlayer.cs` | Per-player public tier + secret tier |
| `PhaseController.cs` | Authoritative phase machine, server clock, run seed, role dealing |
| `RoleId.cs` | Placeholder role enum |
| `NetworkDebugHud.cs` | Throwaway IMGUI harness — delete once real UI exists |

## Two things that are easy to get wrong

**RPCs are not ownership-checked for you.** Any client can invoke an RPC on any spawned
`NetworkObject`. `NetworkPlayer` compares `rpcParams.Receive.SenderClientId` against
`OwnerClientId` before acting; anything you add must do the same.

**Client IDs are not identity.** NGO reassigns them per connection. Reconnection is keyed on the
persistent GUID in `LocalPlayerProfile`, which is what lets a player who crashes mid-run reclaim
their seat *and their role* instead of being dealt a new one.

## Timing

Phase deadlines use `NetworkManager.ServerTime.Time`, not `Time.time`, so a client that joins
late counts down against the same clock as everyone else.

## Setup

`Roguewolf ▸ Setup ▸ Create Network Sandbox Scene` builds the scene and player prefab wired up.
Press Play, Host, then connect a second editor instance or build as a client. `F1` toggles the HUD.

## Not built yet

Voting/night-action resolution, Relay or Lobby integration for play outside LAN (swap
`UnityTransport` connection data for a Relay allocation; nothing above changes), and rate limiting
on client→server RPCs.
