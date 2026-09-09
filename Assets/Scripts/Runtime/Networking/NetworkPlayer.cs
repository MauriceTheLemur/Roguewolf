using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Roguewolf.Networking
{
    /// <summary>
    /// Spawned once per connected client by the NetworkManager's player prefab.
    ///
    /// State is split into two tiers, and that split is the whole point of this class:
    ///   - PUBLIC  variables replicate to everyone (name, seat, alive, ready).
    ///   - SECRET  variables use <c>NetworkVariableReadPermission.Owner</c>, so NGO only ever
    ///             writes them to the owning client and the server. Other clients never receive
    ///             the bytes at all, which means no amount of memory editing or packet sniffing
    ///             on their end reveals the role.
    ///
    /// Never put hidden information in a public variable and rely on the UI to hide it.
    /// </summary>
    public class NetworkPlayer : NetworkBehaviour
    {
        static readonly List<NetworkPlayer> _all = new();

        /// <summary>Every spawned player, on any peer. Order is spawn order, not seat order.</summary>
        public static IReadOnlyList<NetworkPlayer> All => _all;

        public static event Action<NetworkPlayer> Spawned;
        public static event Action<NetworkPlayer> Despawned;

        /// <summary>The local machine's own player, or null before it spawns.</summary>
        public static NetworkPlayer Local { get; private set; }

        // ------------------------------------------------------------ public tier

        public readonly NetworkVariable<FixedString32Bytes> DisplayName = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<int> Seat = new(
            -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<bool> IsReady = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<bool> IsAlive = new(
            true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // ------------------------------------------------------------ secret tier

        /// <summary>
        /// Replicated only to the owner and the server. This is the primitive that makes a
        /// hidden-role game safe on a shared server.
        /// </summary>
        public readonly NetworkVariable<RoleId> Role = new(
            RoleId.None, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

        /// <summary>Raised on the owning client when the server hands it a private message.</summary>
        public event Action<string> SecretReceived;

        public string Name => DisplayName.Value.ToString();

        public override void OnNetworkSpawn()
        {
            _all.Add(this);

            if (IsOwner)
                Local = this;

            if (IsServer)
                PushSessionDataToVariables();

            Spawned?.Invoke(this);
        }

        public override void OnNetworkDespawn()
        {
            _all.Remove(this);

            if (Local == this)
                Local = null;

            Despawned?.Invoke(this);
        }

        /// <summary>
        /// Server-side: seed the replicated variables from the authoritative session record.
        /// On a reconnect this is what restores the player's seat, name, role and alive state.
        /// </summary>
        void PushSessionDataToVariables()
        {
            var data = SessionManager.Instance.GetPlayer(OwnerClientId);
            if (data == null)
            {
                Debug.LogError($"[NetworkPlayer] No session record for client {OwnerClientId}.");
                return;
            }

            DisplayName.Value = new FixedString32Bytes(data.PlayerName);
            Seat.Value = data.Seat;
            IsAlive.Value = data.IsAlive;
            Role.Value = data.Role;
            IsReady.Value = false;
        }

        // ------------------------------------------------------------ server API

        /// <summary>Server-side: assign a role, writing through to the session record.</summary>
        public void ServerSetRole(RoleId role)
        {
            if (!IsServer)
                return;

            Role.Value = role;

            var data = SessionManager.Instance.GetPlayer(OwnerClientId);
            if (data != null)
                data.Role = role;
        }

        /// <summary>Server-side: kill or revive, writing through to the session record.</summary>
        public void ServerSetAlive(bool alive)
        {
            if (!IsServer)
                return;

            IsAlive.Value = alive;

            var data = SessionManager.Instance.GetPlayer(OwnerClientId);
            if (data != null)
                data.IsAlive = alive;
        }

        /// <summary>
        /// Server-side: send a private line to this player alone -- "your fellow wolves are X and Y",
        /// a seer result, a roguelike trait roll. Use this for one-shot facts; use an owner-read
        /// NetworkVariable for anything that must survive a reconnect.
        /// </summary>
        public void ServerTellOwner(string message)
        {
            if (!IsServer)
                return;

            SecretRpc(new FixedString128Bytes(message), RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void SecretRpc(FixedString128Bytes message, RpcParams rpcParams)
        {
            SecretReceived?.Invoke(message.ToString());
        }

        // ------------------------------------------------------------ client -> server

        /// <summary>Ask the server to flip this player's ready flag. Owner only.</summary>
        [Rpc(SendTo.Server)]
        public void RequestSetReadyRpc(bool ready, RpcParams rpcParams = default)
        {
            // Any client can invoke an RPC on any NetworkObject, so ownership is checked here
            // rather than assumed. Without this, one player could ready-up another.
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
                return;

            IsReady.Value = ready;
        }

        /// <summary>Ask the server to change this player's display name. Owner only, lobby only.</summary>
        [Rpc(SendTo.Server)]
        public void RequestSetNameRpc(FixedString32Bytes newName, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
                return;

            if (PhaseController.Instance != null && PhaseController.Instance.Phase != GamePhase.Lobby)
                return;

            var clean = LocalPlayerProfile.Sanitize(newName.ToString());
            DisplayName.Value = new FixedString32Bytes(clean);

            var data = SessionManager.Instance.GetPlayer(OwnerClientId);
            if (data != null)
                data.PlayerName = clean;
        }
    }
}
