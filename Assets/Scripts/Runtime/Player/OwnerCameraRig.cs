using Unity.Netcode;
using UnityEngine;

namespace Roguewolf
{
    /// <summary>
    /// Decides whether this avatar's camera is the one this machine renders and listens through.
    ///
    /// Every client instantiates an avatar for EVERY player in the match, so a six-player game
    /// produces six cameras and six audio listeners on each machine. Exactly one of them -- the
    /// local player's -- should be live, and Unity will warn about the surplus audio listeners.
    ///
    /// Unlike the other player components, this one deliberately does NOT disable itself on
    /// non-owners: switching OFF the other players' cameras is work that only ever happens on the
    /// non-owner copies. That is the reason it is a separate component from PlayerLook rather
    /// than a few lines inside it.
    /// </summary>
    [DisallowMultipleComponent]
    public class OwnerCameraRig : NetworkBehaviour
    {
        [SerializeField, Tooltip("Camera on this avatar. Only the local player's copy stays enabled.")]
        private Camera _camera;

        [SerializeField, Tooltip("Audio listener on the camera. Unity warns when more than one is active.")]
        private AudioListener _audioListener;

        /// <summary>True while this avatar is the one the local machine is looking through.</summary>
        public bool IsLive { get; private set; }

        public override void OnNetworkSpawn()
        {
            SetLive(IsOwner);
        }

        public override void OnNetworkDespawn()
        {
            SetLive(false);
        }

        /// <summary>
        /// Hand the local view to this avatar, or take it away.
        ///
        /// Public so that a future spectator flow can point a dead villager's view at someone
        /// else's avatar without this component needing to know any of the game's rules.
        /// </summary>
        public void SetLive(bool isLive)
        {
            IsLive = isLive;

            // The components are toggled rather than the GameObject, so anything parented to the
            // camera later (a held item, a world-space UI) is not switched off as a side effect.
            if (_camera != null)
                _camera.enabled = isLive;

            if (_audioListener != null)
                _audioListener.enabled = isLive;
        }

        // Editor convenience: fills the references in when the component is first added.
        private void Reset()
        {
            _camera = GetComponentInChildren<Camera>(true);

            if (_camera != null)
                _audioListener = _camera.GetComponent<AudioListener>();
        }
    }
}
