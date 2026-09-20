using Unity.Netcode;
using UnityEngine;

namespace Roguewolf
{
    public class PlayerMovement : NetworkBehaviour
    {
        [SerializeField] private PlayerInputHandler _playerInputHandler;
        [SerializeField] private CharacterController _characterController;
        
        [Header("Movement")]
        [SerializeField, Min(0f)] private float _maxSpeed = 1f;
        [SerializeField, Min(0f), Tooltip("Seconds to reach max speed from a standstill. 0 = instant.")]
        private float _accelerationTime = 0.15f;
        [SerializeField, Min(0f), Tooltip("Seconds to stop from max speed. 0 = instant.")]
        private float _decelerationTime = 0.10f;

        private bool _isPlayerMoveInputReceived = false;
        private Vector2 _currentMovementInput = Vector2.zero;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;

        public override void OnNetworkSpawn()
        {
            enabled = IsOwner;
        }

        private void FixedUpdate()
        {
            UpdateHorizontalVelocity();
            MovePlayer();
        }

        /// <summary>
        /// Steers the current velocity toward the velocity the input is asking for, moving no
        /// further than acceleration (or deceleration) allows within this physics step.
        /// </summary>
        private void UpdateHorizontalVelocity()
        {
            Vector3 inputDirection = transform.forward * _currentMovementInput.y + transform.right * _currentMovementInput.x;
            // Clamped rather than normalized: diagonal keyboard input has a magnitude of ~1.41 and
            // would otherwise outrun cardinal input, but a half-pressed stick should still mean
            // half speed.
            inputDirection = Vector3.ClampMagnitude(inputDirection, 1f);
            
            Vector3 targetVelocity = inputDirection * _maxSpeed;

            float acceleration = _isPlayerMoveInputReceived
                ? GetAcceleration(_maxSpeed, _accelerationTime)
                : GetAcceleration(_maxSpeed, _decelerationTime);

            float incrementedSpeed = acceleration * Time.fixedDeltaTime;
            
            _horizontalVelocity =
                Vector3.MoveTowards(_horizontalVelocity, targetVelocity, incrementedSpeed);
        }

        private void MovePlayer()
        {
            _characterController.Move(_horizontalVelocity * Time.fixedDeltaTime);
        }

        static private float GetAcceleration(float speed, float time)
        {
            if (time > 0)
            {
                return speed / time;
            }
            else
            {
                return float.PositiveInfinity;
            }
        }

        private void JumpPlayer(float jump)
        {
            
        }

        private void ProcessOnMovePerformed(Vector2 moveInput)
        {
            _isPlayerMoveInputReceived = true;
            _currentMovementInput = moveInput;
        }

        private void ProcessOnMoveCancelled(Vector2 moveInput)
        {
            _isPlayerMoveInputReceived = false;
            _currentMovementInput = moveInput;
        }
        private void OnEnable()
        {
            _playerInputHandler.OnMovePerformed += ProcessOnMovePerformed;
            _playerInputHandler.OnMoveCancelled += ProcessOnMoveCancelled;
            _playerInputHandler.OnJump += JumpPlayer;
        }

        private void OnDisable()
        {
            _playerInputHandler.OnMovePerformed -= ProcessOnMovePerformed;
            _playerInputHandler.OnMoveCancelled -= ProcessOnMoveCancelled;
            _playerInputHandler.OnJump -= JumpPlayer;
        }
    }
}
