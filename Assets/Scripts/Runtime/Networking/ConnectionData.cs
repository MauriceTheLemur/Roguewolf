using System;
using UnityEngine;

namespace Roguewolf.Networking
{
    /// <summary>
    /// Sent by a joining client inside <c>NetworkConfig.ConnectionData</c> and read by the
    /// server during connection approval, before the client is allowed to exist.
    /// Keep this small: it has to fit in a single connection packet.
    /// </summary>
    [Serializable]
    public struct ConnectionPayload
    {
        public string PlayerGuid;
        public string PlayerName;
        public int BuildVersion;

        public byte[] Serialize() => System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(this));

        public static bool TryDeserialize(byte[] raw, out ConnectionPayload payload)
        {
            payload = default;
            if (raw == null || raw.Length == 0)
                return false;

            try
            {
                payload = JsonUtility.FromJson<ConnectionPayload>(System.Text.Encoding.UTF8.GetString(raw));
                return !string.IsNullOrEmpty(payload.PlayerGuid);
            }
            catch (Exception)
            {
                // Malformed payloads are an expected hostile input, not a bug. Reject quietly.
                return false;
            }
        }
    }

    /// <summary>
    /// Why the server turned a connection away. Sent back as the approval <c>Reason</c> string
    /// and readable on the client via <c>NetworkManager.Singleton.DisconnectReason</c>.
    /// </summary>
    public enum DisconnectReason
    {
        Unknown = 0,
        ServerFull,
        GameInProgress,
        VersionMismatch,
        BadPayload,
        DuplicateSession,
        KickedByHost,
        HostShutdown
    }

    /// <summary>
    /// The local machine's identity. The GUID persists across sessions so a player who
    /// crashes mid-run can reclaim their seat (and their secret role) instead of being
    /// handed a fresh one. Client IDs cannot do this job -- NGO reassigns them per connection.
    /// </summary>
    public static class LocalPlayerProfile
    {
        const string GuidKey = "roguewolf.player.guid";
        const string NameKey = "roguewolf.player.name";

        static string _guid;
        static string _keySuffix;

        /// <summary>
        /// Appended to every PlayerPrefs key so each editor instance keeps a distinct identity.
        ///
        /// Multiplayer Play Mode runs each virtual player as its own process that symlinks this
        /// project's ProjectSettings, so companyName and productName -- and therefore the single
        /// PlayerPrefs store they resolve to -- are identical across every window. Unsalted, all
        /// of them read the same GUID and the server refuses the second one as a
        /// <see cref="DisconnectReason.DuplicateSession"/>. Each virtual player does launch with
        /// its own -projectPath under Library/VP/&lt;id&gt;, which makes the data path a stable
        /// per-window discriminator that still survives play sessions and domain reloads.
        ///
        /// Empty in a build, where every process already owns its own PlayerPrefs store.
        /// </summary>
        static string KeySuffix
        {
            get
            {
                if (_keySuffix != null)
                    return _keySuffix;

                _keySuffix = Application.isEditor ? "." + ShortHash(Application.dataPath) : string.Empty;
                return _keySuffix;
            }
        }

        public static string Guid
        {
            get
            {
                if (!string.IsNullOrEmpty(_guid))
                    return _guid;

                var key = GuidKey + KeySuffix;

                _guid = PlayerPrefs.GetString(key, string.Empty);
                if (string.IsNullOrEmpty(_guid))
                {
                    _guid = System.Guid.NewGuid().ToString();
                    PlayerPrefs.SetString(key, _guid);
                    PlayerPrefs.Save();
                }

                return _guid;
            }
        }

        public static string PlayerName
        {
            get
            {
                var stored = PlayerPrefs.GetString(NameKey + KeySuffix, string.Empty);
                return string.IsNullOrWhiteSpace(stored) ? "Wolf" + UnityEngine.Random.Range(100, 999) : stored;
            }
            set
            {
                PlayerPrefs.SetString(NameKey + KeySuffix, Sanitize(value));
                PlayerPrefs.Save();
            }
        }

        /// <summary>FNV-1a. Keeps the salted key short and safe for the registry-backed store.</summary>
        static string ShortHash(string value)
        {
            unchecked
            {
                var hash = 2166136261u;
                foreach (var c in value)
                {
                    hash ^= c;
                    hash *= 16777619u;
                }

                return hash.ToString("x8");
            }
        }

        /// <summary>
        /// Names arrive from the network and end up on screen. Clamp length and strip control
        /// characters here so no other system has to trust the string.
        /// </summary>
        public static string Sanitize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "Player";

            var trimmed = raw.Trim();
            var buffer = new System.Text.StringBuilder(NetworkConstants.MaxNameLength);
            foreach (var c in trimmed)
            {
                if (buffer.Length >= NetworkConstants.MaxNameLength)
                    break;
                if (!char.IsControl(c))
                    buffer.Append(c);
            }

            return buffer.Length == 0 ? "Player" : buffer.ToString();
        }
    }

    public static class NetworkConstants
    {
        /// <summary>Bump on any change to RPC signatures or replicated layouts so old builds bounce.</summary>
        public const int BuildVersion = 1;

        public const int MaxNameLength = 16;
        public const ushort DefaultPort = 7777;
        public const string DefaultAddress = "127.0.0.1";

        /// <summary>Listen address that accepts connections from other machines, not just this one.</summary>
        public const string AllInterfaces = "0.0.0.0";
    }
}
