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
        
        [Header("Gravity")]
        [SerializeField, Min(0f)] private float _gravityAcceleration = 10f;
        [SerializeField, Min(0f), Tooltip("Fastest the player can fall. Caps acceleration so a long drop can't outrun collision.")] 
        private float _terminalVelocity = 50f;
        [SerializeField, Min(0f), Tooltip("Small constant downward speed held while grounded so isGrounded stays true.")]
        private float _groundedStickSpeed = 2f;
        [SerializeField, Min(0f)] private float _jumpHeight = 1f;

        private bool _isPlayerMoveInputReceived = false;
        private bool _isPlayerJumpInputReceived = false;
        private Vector2 _currentMovementInput = Vector2.zero;
        private Vector3 _horizontalVelocity;
        private Vector3 _verticalVelocity;
        
        private static readonly Vector3 GravityDirection = Vector3.down;

        public override void OnNetworkSpawn()
        {
            enabled = IsOwner;
        }

        private void FixedUpdate()
        {
            UpdateHorizontalVelocity();
            UpdateVerticalVelocity();
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

        private void UpdateVerticalVelocity()
        {
            bool isGrounded = _characterController.isGrounded;
            
            if (isGrounded && _isPlayerJumpInputReceived)
            {
                // v = sqrt(2 * g * h): the launch speed that peaks at exactly _jumpHeight.
                _verticalVelocity = -GravityDirection * Mathf.Sqrt(2f * _gravityAcceleration * _jumpHeight);
                return;
            }
            
            _verticalVelocity = Vector3.MoveTowards(
                _verticalVelocity,
                GravityDirection * _terminalVelocity,
                _gravityAcceleration * Time.fixedDeltaTime);
            
            
        }

        private void MovePlayer()
        {
            _characterController.Move((_horizontalVelocity + _verticalVelocity) * Time.fixedDeltaTime);
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

        private void ProcessOnJumpPerformed(float jumpInput)
        {
            _isPlayerJumpInputReceived = true;
        }
        private void ProcessOnJumpCancelled(float jumpInput)
        {
            _isPlayerJumpInputReceived = false;
        }
        
        private void OnEnable()
        {
            _playerInputHandler.OnMovePerformed += ProcessOnMovePerformed;
            _playerInputHandler.OnMoveCancelled += ProcessOnMoveCancelled;
            _playerInputHandler.OnJumpPerformed += ProcessOnJumpPerformed;
            _playerInputHandler.OnJumpCancelled += ProcessOnJumpCancelled;
        }

        private void OnDisable()
        {
            _playerInputHandler.OnMovePerformed -= ProcessOnMovePerformed;
            _playerInputHandler.OnMoveCancelled -= ProcessOnMoveCancelled;
            _playerInputHandler.OnJumpPerformed -= ProcessOnJumpPerformed;
            _playerInputHandler.OnJumpCancelled -= ProcessOnJumpCancelled;
        }
    }
}
