using System.Collections.Generic;
using UnityEngine;

namespace Roguewolf.Networking
{
    /// <summary>
    /// Server-side record for one seat at the table. Survives that player's disconnection
    /// so their role and alive/dead status can be restored if they come back.
    /// This never leaves the server -- it is deliberately not an INetworkSerializable.
    /// </summary>
    public class SessionPlayerData
    {
        public string PlayerGuid;
        public string PlayerName;
        public int Seat;
        public ulong ClientId;
        public bool IsConnected;

        /// <summary>Secret state preserved across a reconnect. Never replicated wholesale.</summary>
        public RoleId Role = RoleId.None;
        public bool IsAlive = true;
    }

    /// <summary>
    /// Owns the mapping between persistent player GUIDs and the transient client IDs NGO
    /// hands out. Lives only on the server; clients never construct one.
    /// </summary>
    public class SessionManager
    {
        public static SessionManager Instance { get; } = new SessionManager();

        readonly Dictionary<string, SessionPlayerData> _playersByGuid = new();
        readonly Dictionary<ulong, string> _guidByClientId = new();

        SessionManager() { }

        public IEnumerable<SessionPlayerData> AllPlayers => _playersByGuid.Values;

        public int SeatedCount
        {
            get
            {
                var count = 0;
                foreach (var p in _playersByGuid.Values)
                {
                    if (p.IsConnected)
                        count++;
                }

                return count;
            }
        }

        public SessionPlayerData GetPlayer(ulong clientId)
        {
            return _guidByClientId.TryGetValue(clientId, out var guid) && _playersByGuid.TryGetValue(guid, out var data)
                ? data
                : null;
        }

        public SessionPlayerData GetPlayerByGuid(string guid)
        {
            return guid != null && _playersByGuid.TryGetValue(guid, out var data) ? data : null;
        }

        /// <summary>
        /// True when this GUID already has a live connection. A second connection from the
        /// same GUID is either a duplicate launch or someone trying to occupy two seats.
        /// </summary>
        public bool IsGuidAlreadyConnected(string guid)
        {
            return _playersByGuid.TryGetValue(guid, out var existing) && existing.IsConnected;
        }

        /// <summary>
        /// Claims a seat for a newly approved connection, reusing the previous record when the
        /// GUID is known. Returns the seated record.
        /// </summary>
        public SessionPlayerData SeatPlayer(ulong clientId, string guid, string playerName)
        {
            if (_playersByGuid.TryGetValue(guid, out var existing))
            {
                // Reconnect: keep seat, role and alive state, adopt the new client id.
                _guidByClientId.Remove(existing.ClientId);
                existing.ClientId = clientId;
                existing.IsConnected = true;
                existing.PlayerName = playerName;
                _guidByClientId[clientId] = guid;
                return existing;
            }

            var data = new SessionPlayerData
            {
                PlayerGuid = guid,
                PlayerName = playerName,
                Seat = NextFreeSeat(),
                ClientId = clientId,
                IsConnected = true
            };

            _playersByGuid[guid] = data;
            _guidByClientId[clientId] = guid;
            return data;
        }

        /// <summary>
        /// Marks a player offline. During a run the seat is held open for reconnection;
        /// in the lobby the record is dropped entirely so the slot frees up.
        /// </summary>
        public void MarkDisconnected(ulong clientId, bool holdSeatOpen)
        {
            if (!_guidByClientId.TryGetValue(clientId, out var guid))
                return;

            _guidByClientId.Remove(clientId);

            if (!_playersByGuid.TryGetValue(guid, out var data))
                return;

            data.IsConnected = false;

            if (!holdSeatOpen)
                _playersByGuid.Remove(guid);
        }

        /// <summary>Clears everything. Call when the server shuts down or a run is abandoned.</summary>
        public void Reset()
        {
            _playersByGuid.Clear();
            _guidByClientId.Clear();
        }

        int NextFreeSeat()
        {
            for (var seat = 0; seat < 64; seat++)
            {
                var taken = false;
                foreach (var p in _playersByGuid.Values)
                {
                    if (p.Seat != seat)
                        continue;
                    taken = true;
                    break;
                }

                if (!taken)
                    return seat;
            }

            Debug.LogError("[SessionManager] Ran out of seats.");
            return -1;
        }
    }
}
