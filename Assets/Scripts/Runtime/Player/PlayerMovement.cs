using System;
using Unity.Netcode;
using UnityEngine;

namespace Roguewolf
{
    public class PlayerMovement : NetworkBehaviour
    {
        [SerializeField] private PlayerInputHandler _playerInputHandler;
        [SerializeField] private CharacterController _characterController;
        
        [Header("Movement")]
        [SerializeField, Min(0f)] private float _maxSpeed = 6f;
        [SerializeField, Min(0f), Tooltip("Seconds to reach max speed from a standstill. 0 = instant.")]
        private float _accelerationTime = 0.15f;
        [SerializeField, Min(0f), Tooltip("Seconds to stop from max speed. 0 = instant.")]
        private float _decelerationTime = 0.10f;
        
        
// m/s², derived every access. Infinity = snap, which MoveTowards handles natively.
        private float Acceleration => _accelerationTime > 0f ? _maxSpeed / _accelerationTime : Mathf.Infinity;
        private float Deceleration => _decelerationTime > 0f ? _maxSpeed / _decelerationTime : Mathf.Infinity;

        private bool _isPlayerMove = false;
        private Vector2 _currentMovement = Vector2.zero;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;

        public override void OnNetworkSpawn()
        {
            enabled = IsOwner;
        }
        
        

        private void FixedUpdate()
        {
            if (_isPlayerMove)
            {
                MovePlayer(_currentMovement);
            }
            
        }

        private void MovePlayer(Vector2 moveInput)
        {
            Vector3 movement = new Vector3(moveInput.x, 0, moveInput.y);
            _characterController.Move(movement);
        }
        

        private void JumpPlayer(float jump)
        {
            
        }

        private void ProcessOnMovePerformed(Vector2 moveInput)
        {
            _isPlayerMove = true;
            _currentMovement = moveInput;
        }

        private void ProcessOnMoveCancelled(Vector2 moveInput)
        {
            _isPlayerMove = false;
            _currentMovement = moveInput;
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
