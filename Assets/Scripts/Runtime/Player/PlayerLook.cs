using System;
using Unity.Netcode;
using UnityEngine;

namespace Roguewolf
{
    /// <summary>
    /// First-person look for the owning client.
    ///
    /// The rotation is split across two transforms, and the split follows what does and does not
    /// travel over the network:
    ///
    ///   YAW (left/right) is applied to the root, which carries the owner-authoritative
    ///   NetworkTransform. Every other player therefore sees which way this player is facing --
    ///   in a social deduction game, who is looking at whom is information worth replicating.
    ///
    ///   PITCH (up/down) is applied to the camera alone and never leaves this machine. It is also
    ///   clamped, because rotating the root on X would tip the CharacterController capsule over
    ///   and break both movement and collision.
    ///
    /// Deciding which camera is actually rendering is a separate concern -- see OwnerCameraRig.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerLook : NetworkBehaviour
    {
        [SerializeField] private PlayerInputHandler _playerInputHandler;

        [SerializeField, Tooltip("Transform that pitch is applied to: the camera itself, or a pivot above it.")]
        private Transform _cameraTransform;

        [Header("Sensitivity")] [SerializeField, Min(0f), Tooltip("Degrees turned per pixel of mouse movement.")]
        private float _mouseSensitivity = 0.1f;
        
        [Header("Limits")]
        [SerializeField, Range(0f, 90f), Tooltip("How far up or down the camera can tilt before it clamps.")]
        private float _maxPitch = 89f;

        private float _pitch;

        public override void OnNetworkSpawn()
        {
            // Only the owning client drives its own view. Every other copy of this avatar gets its
            // yaw from the NetworkTransform instead, so this component has nothing to do there.
            enabled = IsOwner;

            if (!IsOwner) return;

            if (_playerInputHandler == null || _cameraTransform == null)
            {
                Debug.LogError($"[PlayerLook] Missing references on '{name}'. Assign them on the prefab.", this);
                enabled = false;
                return;
            }

            SetCursorLocked(true);
            
            
            _playerInputHandler.OnLookPerformed += Look;
            _playerInputHandler.OnLookCancelled += Look;
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner)
                return;

            SetCursorLocked(false);
            
            _playerInputHandler.OnLookPerformed -= Look;
            _playerInputHandler.OnLookCancelled -= Look;
        }
        
        private void Look(Vector2 lookInput)
        {
            if (lookInput == Vector2.zero)
                return;

            Vector2 lookDelta = lookInput * _mouseSensitivity;

            ApplyYaw(lookDelta.x);
            ApplyPitch(lookDelta.y);
        }

        // Space.World rather than Space.Self so that yaw stays a turn around the world's up axis
        // even if something else ever tilts the body.
        private void ApplyYaw(float yawDelta)
        {
            transform.Rotate(Vector3.up, yawDelta, Space.World);
        }

        private void ApplyPitch(float pitchDelta)
        {
            // Subtracted: pushing the mouse forward reads as positive Y and should look UP, but a
            // positive X euler angle points the camera DOWN.
            _pitch = Mathf.Clamp(_pitch - pitchDelta, -_maxPitch, _maxPitch);
            _cameraTransform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        /// <summary>
        /// TEMPORARY HOME. Cursor state is global to the application, not per-player, and the
        /// voting and role screens will need it unlocked while the player is alive and still able
        /// to look around. Move this to a gameplay-vs-UI input mode controller once that UI
        /// exists; it is kept behind this one call so that move is a small change.
        /// </summary>
        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
