using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace Roguewolf.Networking
{
    public enum GamePhase : byte
    {
        Lobby = 0,
        RoleReveal = 1,
        Night = 2,
        Day = 3,
        Vote = 4,
        RoundEnd = 5,
        RunOver = 6
    }

    /// <summary>
    /// The authoritative clock and state machine for a run. Placed in the scene as a single
    /// NetworkObject.
    ///
    /// Every phase transition happens on the server. Clients read <see cref="Phase"/> and
    /// <see cref="PhaseEndTime"/> and render from them -- they never decide that night is over.
    /// Deadlines are expressed in NGO's <c>ServerTime</c> rather than <c>Time.time</c> so every
    /// peer counts down against the same clock regardless of when it joined.
    /// </summary>
    public class PhaseController : NetworkBehaviour
    {
        public static PhaseController Instance { get; private set; }

        [Header("Phase durations (seconds, 0 = untimed)")]
        [SerializeField, FormerlySerializedAs("m_RoleRevealDuration")] private float _roleRevealDuration = 8f;
        [SerializeField, FormerlySerializedAs("m_NightDuration")] private float _nightDuration = 30f;
        [SerializeField, FormerlySerializedAs("m_DayDuration")] private float _dayDuration = 90f;
        [SerializeField, FormerlySerializedAs("m_VoteDuration")] private float _voteDuration = 30f;
        [SerializeField, FormerlySerializedAs("m_RoundEndDuration")] private float _roundEndDuration = 5f;

        [Header("Rules")]
        [SerializeField, FormerlySerializedAs("m_MinPlayersToStart")] private int _minPlayersToStart = 3;

        readonly NetworkVariable<GamePhase> _phase = new(
            GamePhase.Lobby, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        readonly NetworkVariable<int> _round = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Server time at which the current phase expires. 0 means the phase is untimed.</summary>
        readonly NetworkVariable<double> _phaseEndTime = new(
            0d, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>
        /// The run's master seed. Replicated once at run start so every client can generate
        /// identical roguelike content locally instead of the server streaming it all down.
        /// Anything derived from this seed that must stay hidden still has to be gated
        /// server-side -- a shared seed is shared knowledge.
        /// </summary>
        readonly NetworkVariable<int> _runSeed = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public GamePhase Phase => _phase.Value;
        public int Round => _round.Value;
        public int RunSeed => _runSeed.Value;
        public double PhaseEndTime => _phaseEndTime.Value;

        /// <summary>Seconds left in the phase, or -1 when the phase is untimed.</summary>
        public float TimeRemaining
        {
            get
            {
                if (_phaseEndTime.Value <= 0d || NetworkManager == null || !NetworkManager.IsListening)
                    return -1f;

                return Mathf.Max(0f, (float)(_phaseEndTime.Value - NetworkManager.ServerTime.Time));
            }
        }

        /// <summary>Raised on every peer when the phase changes. (previous, current)</summary>
        public event Action<GamePhase, GamePhase> PhaseChanged;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            _phase.OnValueChanged += HandlePhaseChanged;

            if (IsServer)
                ApplyLobbyGate(_phase.Value);
        }

        public override void OnNetworkDespawn()
        {
            _phase.OnValueChanged -= HandlePhaseChanged;

            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (!IsSpawned || !IsServer)
                return;

            if (_phaseEndTime.Value <= 0d)
                return;

            if (NetworkManager.ServerTime.Time >= _phaseEndTime.Value)
                AdvancePhase();
        }

        // ------------------------------------------------------------ server control

        /// <summary>True when the lobby has enough ready players to begin.</summary>
        public bool CanStartRun(out string blocker)
        {
            blocker = null;

            if (_phase.Value != GamePhase.Lobby)
            {
                blocker = "A run is already in progress.";
                return false;
            }

            var connected = 0;
            var ready = 0;
            foreach (var player in NetworkPlayer.All)
            {
                connected++;
                if (player.IsReady.Value)
                    ready++;
            }

            if (connected < _minPlayersToStart)
            {
                blocker = $"Need {_minPlayersToStart} players, have {connected}.";
                return false;
            }

            if (ready < connected)
            {
                blocker = $"{connected - ready} player(s) not ready.";
                return false;
            }

            return true;
        }

        /// <summary>Server-side: begin a run. Rolls the seed, assigns roles, opens role reveal.</summary>
        public void StartRun()
        {
            if (!IsServer || _phase.Value != GamePhase.Lobby)
                return;

            if (!CanStartRun(out var blocker))
            {
                Debug.Log($"[PhaseController] Cannot start: {blocker}");
                return;
            }

            _runSeed.Value = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            _round.Value = 1;

            AssignRoles(_runSeed.Value);
            SetPhase(GamePhase.RoleReveal);
        }

        /// <summary>Server-side: force the current phase to end now.</summary>
        public void AdvancePhase()
        {
            if (!IsServer)
                return;

            var next = _phase.Value switch
            {
                GamePhase.RoleReveal => GamePhase.Night,
                GamePhase.Night => GamePhase.Day,
                GamePhase.Day => GamePhase.Vote,
                GamePhase.Vote => GamePhase.RoundEnd,
                GamePhase.RoundEnd => GamePhase.Night,
                _ => _phase.Value
            };

            if (next == GamePhase.Night && _phase.Value == GamePhase.RoundEnd)
                _round.Value++;

            SetPhase(next);
        }

        /// <summary>Server-side: tear the run down and return everyone to the lobby.</summary>
        public void ReturnToLobby()
        {
            if (!IsServer)
                return;

            foreach (var player in NetworkPlayer.All)
            {
                player.ServerSetRole(RoleId.None);
                player.ServerSetAlive(true);
                player.IsReady.Value = false;
            }

            _round.Value = 0;
            _runSeed.Value = 0;
            SetPhase(GamePhase.Lobby);
        }

        void SetPhase(GamePhase phase)
        {
            _phase.Value = phase;

            var duration = DurationFor(phase);
            _phaseEndTime.Value = duration > 0f ? NetworkManager.ServerTime.Time + duration : 0d;

            ApplyLobbyGate(phase);
        }

        float DurationFor(GamePhase phase) => phase switch
        {
            GamePhase.RoleReveal => _roleRevealDuration,
            GamePhase.Night => _nightDuration,
            GamePhase.Day => _dayDuration,
            GamePhase.Vote => _voteDuration,
            GamePhase.RoundEnd => _roundEndDuration,
            _ => 0f
        };

        /// <summary>Close the door to brand-new players once a run is under way.</summary>
        void ApplyLobbyGate(GamePhase phase)
        {
            if (ConnectionManager.Instance != null)
                ConnectionManager.Instance.AcceptingNewPlayers = phase == GamePhase.Lobby;
        }

        void HandlePhaseChanged(GamePhase previous, GamePhase current)
        {
            PhaseChanged?.Invoke(previous, current);
        }

        // ------------------------------------------------------------ role dealing

        /// <summary>
        /// Placeholder deal: one wolf per four players, one seer, rest villagers. Replace with the
        /// real roguelike draft later -- what matters here is that it runs server-side only and
        /// writes into owner-read variables, so no client ever sees another player's result.
        /// </summary>
        void AssignRoles(int seed)
        {
            var players = new System.Collections.Generic.List<NetworkPlayer>(NetworkPlayer.All);
            if (players.Count == 0)
                return;

            // Deterministic shuffle from the run seed, using an isolated RNG so this does not
            // disturb the shared UnityEngine.Random sequence.
            var rng = new System.Random(seed);
            for (var i = players.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (players[i], players[j]) = (players[j], players[i]);
            }

            var wolfCount = Mathf.Max(1, players.Count / 4);
            var wolves = new System.Collections.Generic.List<NetworkPlayer>();

            for (var i = 0; i < players.Count; i++)
            {
                RoleId role;
                if (i < wolfCount)
                {
                    role = RoleId.Werewolf;
                    wolves.Add(players[i]);
                }
                else if (i == wolfCount && players.Count >= 4)
                {
                    role = RoleId.Seer;
                }
                else
                {
                    role = RoleId.Villager;
                }

                players[i].ServerSetRole(role);
            }

            // Wolves learn each other -- a targeted RPC, so the packet only goes to those clients.
            if (wolves.Count <= 1)
                return;

            foreach (var wolf in wolves)
            {
                var others = new System.Text.StringBuilder();
                foreach (var other in wolves)
                {
                    if (other == wolf)
                        continue;
                    if (others.Length > 0)
                        others.Append(", ");
                    others.Append(other.Name);
                }

                wolf.ServerTellOwner($"Your pack: {others}");
            }
        }
    }
}
