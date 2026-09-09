using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Serialization;

namespace Roguewolf.Networking
{
    public enum ConnectionState
    {
        Offline,
        Connecting,
        ClientConnected,
        Hosting
    }

    /// <summary>
    /// The single entry point for starting/stopping the session and for vetting who gets in.
    /// Sits on the same GameObject as the NetworkManager.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class ConnectionManager : MonoBehaviour
    {
        public static ConnectionManager Instance { get; private set; }

        [Header("Session")]
        [SerializeField, FormerlySerializedAs("m_MaxPlayers")] private int _maxPlayers = 10;

        [Header("Transport defaults")]
        [SerializeField, FormerlySerializedAs("m_Address")] private string _address = NetworkConstants.DefaultAddress;
        [SerializeField, FormerlySerializedAs("m_Port")] private ushort _port = NetworkConstants.DefaultPort;

        /// <summary>
        /// The interface the host binds to, which is not the same question as the address clients
        /// dial. Loopback accepts connections from this machine only -- enough for Multiplayer
        /// Play Mode, but invisible to a build on a second machine. Use
        /// <see cref="NetworkConstants.AllInterfaces"/> to accept from the LAN.
        /// </summary>
        [SerializeField] private string _listenAddress = NetworkConstants.DefaultAddress;

        NetworkManager _networkManager;
        UnityTransport _transport;

        public ConnectionState State { get; private set; } = ConnectionState.Offline;
        public int MaxPlayers => _maxPlayers;
        public string Address => _address;
        public ushort Port => _port;

        /// <summary>
        /// Gate for players the server has never seen. Set false once a run starts so latecomers
        /// bounce, while known GUIDs can still reconnect into their existing seat.
        /// <see cref="PhaseController"/> drives this.
        /// </summary>
        public bool AcceptingNewPlayers { get; set; } = true;

        public event Action<ConnectionState> StateChanged;

        /// <summary>Fired on the local client when it is refused or dropped, with the server's reason.</summary>
        public event Action<DisconnectReason> Disconnected;

        /// <summary>Server-side: a client finished connecting and has been seated.</summary>
        public event Action<SessionPlayerData> PlayerSeated;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            _networkManager = GetComponent<NetworkManager>();
            _transport = GetComponent<UnityTransport>();

            _networkManager.OnConnectionEvent += HandleConnectionEvent;
            _networkManager.OnServerStopped += HandleServerStopped;
            _networkManager.OnClientStopped += HandleClientStopped;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (_networkManager == null)
                return;

            _networkManager.OnConnectionEvent -= HandleConnectionEvent;
            _networkManager.OnServerStopped -= HandleServerStopped;
            _networkManager.OnClientStopped -= HandleClientStopped;
            _networkManager.ConnectionApprovalCallback -= ApproveConnection;
        }

        // ---------------------------------------------------------------- start / stop

        public bool StartHost(string playerName = null, string address = null, ushort port = 0)
        {
            if (State != ConnectionState.Offline)
                return false;

            Configure(address, port, listening: true);
            SessionManager.Instance.Reset();
            AcceptingNewPlayers = true;

            _networkManager.NetworkConfig.ConnectionApproval = true;
            _networkManager.ConnectionApprovalCallback -= ApproveConnection;
            _networkManager.ConnectionApprovalCallback += ApproveConnection;

            // The host runs through approval too, so it needs a payload like anyone else.
            _networkManager.NetworkConfig.ConnectionData = BuildPayload(playerName);

            if (!_networkManager.StartHost())
            {
                Debug.LogError("[ConnectionManager] StartHost failed.");
                return false;
            }

            SetState(ConnectionState.Hosting);
            return true;
        }

        public bool StartClient(string playerName = null, string address = null, ushort port = 0)
        {
            if (State != ConnectionState.Offline)
                return false;

            Configure(address, port, listening: false);

            _networkManager.NetworkConfig.ConnectionApproval = true;
            _networkManager.NetworkConfig.ConnectionData = BuildPayload(playerName);

            if (!_networkManager.StartClient())
            {
                Debug.LogError("[ConnectionManager] StartClient failed.");
                return false;
            }

            SetState(ConnectionState.Connecting);
            return true;
        }

        public void Shutdown()
        {
            if (State == ConnectionState.Offline)
                return;

            _networkManager.Shutdown();
            SetState(ConnectionState.Offline);
        }

        /// <summary>Server-side kick. The reason reaches the client as its DisconnectReason.</summary>
        public void Kick(ulong clientId, DisconnectReason reason = DisconnectReason.KickedByHost)
        {
            if (!_networkManager.IsServer || clientId == NetworkManager.ServerClientId)
                return;

            _networkManager.DisconnectClient(clientId, reason.ToString());
        }

        void Configure(string address, ushort port, bool listening)
        {
            if (!string.IsNullOrWhiteSpace(address))
                _address = address.Trim();
            if (port != 0)
                _port = port;

            if (_transport == null)
                return;

            // A client binds an ephemeral local port and dials _address; only a host needs to be
            // told which interface to listen on.
            if (listening)
                _transport.SetConnectionData(_address, _port, _listenAddress);
            else
                _transport.SetConnectionData(_address, _port);
        }

        byte[] BuildPayload(string playerName)
        {
            if (!string.IsNullOrWhiteSpace(playerName))
                LocalPlayerProfile.PlayerName = playerName;

            return new ConnectionPayload
            {
                PlayerGuid = LocalPlayerProfile.Guid,
                PlayerName = LocalPlayerProfile.PlayerName,
                BuildVersion = NetworkConstants.BuildVersion
            }.Serialize();
        }

        // ---------------------------------------------------------------- approval

        /// <summary>
        /// Runs on the server for every incoming connection, including the host's own.
        /// Everything in <paramref name="request"/> is attacker-controlled -- validate, don't trust.
        /// </summary>
        void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = true;
            response.Pending = false;

            if (!ConnectionPayload.TryDeserialize(request.Payload, out var payload))
            {
                Reject(response, DisconnectReason.BadPayload);
                return;
            }

            if (payload.BuildVersion != NetworkConstants.BuildVersion)
            {
                Reject(response, DisconnectReason.VersionMismatch);
                return;
            }

            var session = SessionManager.Instance;
            var known = session.GetPlayerByGuid(payload.PlayerGuid);

            if (session.IsGuidAlreadyConnected(payload.PlayerGuid))
            {
                Reject(response, DisconnectReason.DuplicateSession);
                return;
            }

            // A returning player reclaims their seat even after the lobby has closed.
            var isReconnect = known != null;

            if (!isReconnect)
            {
                if (!AcceptingNewPlayers)
                {
                    Reject(response, DisconnectReason.GameInProgress);
                    return;
                }

                if (session.SeatedCount >= _maxPlayers)
                {
                    Reject(response, DisconnectReason.ServerFull);
                    return;
                }
            }

            var seated = session.SeatPlayer(
                request.ClientNetworkId,
                payload.PlayerGuid,
                LocalPlayerProfile.Sanitize(payload.PlayerName));

            response.Approved = true;
            PlayerSeated?.Invoke(seated);
        }

        static void Reject(NetworkManager.ConnectionApprovalResponse response, DisconnectReason reason)
        {
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Reason = reason.ToString();
        }

        // ---------------------------------------------------------------- callbacks

        void HandleConnectionEvent(NetworkManager manager, ConnectionEventData data)
        {
            switch (data.EventType)
            {
                case ConnectionEvent.ClientConnected:
                    // Fires locally on this peer once it is fully connected.
                    if (!manager.IsServer)
                        SetState(ConnectionState.ClientConnected);
                    break;

                case ConnectionEvent.ClientDisconnected:
                    if (manager.IsServer)
                    {
                        // Hold the seat open mid-run so the player can reclaim it.
                        SessionManager.Instance.MarkDisconnected(data.ClientId, holdSeatOpen: !AcceptingNewPlayers);
                    }

                    break;
            }
        }

        void HandleServerStopped(bool wasHost)
        {
            SessionManager.Instance.Reset();
            SetState(ConnectionState.Offline);
        }

        void HandleClientStopped(bool wasHost)
        {
            if (wasHost)
                return;

            var reason = ParseReason(_networkManager.DisconnectReason);
            SetState(ConnectionState.Offline);
            Disconnected?.Invoke(reason);
        }

        static DisconnectReason ParseReason(string raw)
        {
            return Enum.TryParse<DisconnectReason>(raw, out var parsed) ? parsed : DisconnectReason.Unknown;
        }

        void SetState(ConnectionState state)
        {
            if (State == state)
                return;

            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
