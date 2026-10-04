using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace Roguewolf.Networking
{
    /// <summary>
    /// Throwaway IMGUI panel for driving the session while there is no real UI. Lets you host,
    /// join, rename and ready up from two editor instances or builds.
    /// Delete this once the game has actual screens -- it is a harness, not a feature.
    /// </summary>
    public class NetworkDebugHud : MonoBehaviour
    {
        [SerializeField, FormerlySerializedAs("m_Visible")] private bool _visible = true;
        [SerializeField, FormerlySerializedAs("m_ToggleKey")] private KeyCode _toggleKey = KeyCode.F1;

        string _address = NetworkConstants.DefaultAddress;
        string _port = NetworkConstants.DefaultPort.ToString();
        string _name;
        string _status = string.Empty;
        string _secret = string.Empty;
        Vector2 _scroll;

        void Awake()
        {
            _name = LocalPlayerProfile.PlayerName;
        }

        void OnEnable()
        {
            NetworkPlayer.Spawned += HandlePlayerSpawned;

            if (ConnectionManager.Instance != null)
                ConnectionManager.Instance.Disconnected += HandleDisconnected;
        }

        void OnDisable()
        {
            NetworkPlayer.Spawned -= HandlePlayerSpawned;

            if (ConnectionManager.Instance != null)
                ConnectionManager.Instance.Disconnected -= HandleDisconnected;
        }

        void HandlePlayerSpawned(NetworkPlayer player)
        {
            if (!player.IsOwner)
                return;

            player.SecretReceived += secret => _secret = secret;
        }

        void HandleDisconnected(DisconnectReason reason)
        {
            _status = $"Disconnected: {reason}";
            _secret = string.Empty;
        }

        void OnGUI()
        {
            // Read the toggle from the IMGUI event stream rather than UnityEngine.Input: this
            // project runs the new Input System backend, where the legacy Input class throws.
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == _toggleKey)
            {
                _visible = !_visible;
                Event.current.Use();
            }

            if (!_visible)
                return;

            var manager = NetworkManager.Singleton;
            var connection = ConnectionManager.Instance;
            if (manager == null || connection == null)
                return;

            GUILayout.BeginArea(new Rect(10, 10, 330, Screen.height - 20), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label($"<b>Roguewolf net ({_toggleKey} to hide)</b>", RichLabel());

            if (!manager.IsListening)
                DrawOfflinePanel(connection);
            else
                DrawOnlinePanel(manager, connection);

            if (!string.IsNullOrEmpty(_status))
                GUILayout.Label(_status);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawOfflinePanel(ConnectionManager connection)
        {
            GUILayout.Label("Name");
            _name = GUILayout.TextField(_name, NetworkConstants.MaxNameLength);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Addr", GUILayout.Width(40));
            _address = GUILayout.TextField(_address);
            GUILayout.Label("Port", GUILayout.Width(34));
            _port = GUILayout.TextField(_port, GUILayout.Width(52));
            GUILayout.EndHorizontal();

            var portOk = ushort.TryParse(_port, out var port) && port != 0;

            // A rejected port silently falls back to the serialized default inside
            // ConnectionManager.Configure, which is a confusing way to end up on the wrong port.
            if (!portOk && !string.IsNullOrWhiteSpace(_port))
                GUILayout.Label($"Bad port -- will use {connection.Port}.");

            if (GUILayout.Button("Host"))
            {
                // Report the result. A failed start leaves the network manager offline, so no
                // callback ever fires to correct an optimistic status line -- it would read as a
                // hang forever.
                _status = connection.StartHost(_name, _address, port)
                    ? string.Empty
                    : "Host failed to start -- see Console.";
            }

            if (GUILayout.Button("Join"))
            {
                _status = connection.StartClient(_name, _address, port)
                    ? "Connecting..."
                    : "Join failed to start -- see Console.";
            }
        }

        void DrawOnlinePanel(NetworkManager manager, ConnectionManager connection)
        {
            var role = manager.IsServer ? "Host" : "Client";

            GUILayout.Label($"{role}  |  clientId {manager.LocalClientId}  |  {connection.State}");

            // Transport is up but the handshake has not finished -- either the host has not
            // approved yet, or nothing is listening there and UTP is still burning its retries.
            if (connection.State == ConnectionState.Connecting)
                GUILayout.Label($"Handshaking with {connection.Address}:{connection.Port}...");

            GUILayout.Space(4);
            DrawLocalPlayer();

            GUILayout.Space(4);
            GUILayout.Label("<b>Table</b>", RichLabel());
            foreach (var player in NetworkPlayer.All)
            {
                var flags = player.IsReady.Value ? " [ready]" : " [...]";
                var you = player.IsOwner ? " (you)" : string.Empty;

                GUILayout.Label($"{player.Seat.Value}. {player.Name}{you}{flags}", RichLabel());
            }

            GUILayout.Space(6);
            if (GUILayout.Button("Disconnect"))
            {
                connection.Shutdown();
                _secret = string.Empty;
                _status = string.Empty;
            }
        }

        void DrawLocalPlayer()
        {
            var local = NetworkPlayer.Local;
            if (local == null)
            {
                GUILayout.Label("Waiting for player object...");
                return;
            }

            var label = local.IsReady.Value ? "Un-ready" : "Ready up";
            if (GUILayout.Button(label))
                local.RequestSetReadyRpc(!local.IsReady.Value);

            GUILayout.BeginHorizontal();
            _name = GUILayout.TextField(_name, NetworkConstants.MaxNameLength);
            if (GUILayout.Button("Rename", GUILayout.Width(64)))
            {
                LocalPlayerProfile.PlayerName = _name;
                local.RequestSetNameRpc(new FixedString32Bytes(LocalPlayerProfile.PlayerName));
            }

            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_secret))
                GUILayout.Label($"Secret: {_secret}");
        }

        static GUIStyle _richLabel;

        static GUIStyle RichLabel()
        {
            return _richLabel ??= new GUIStyle(GUI.skin.label) { richText = true };
        }
    }
}
