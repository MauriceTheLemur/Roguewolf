using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Roguewolf
{
    public class PlayerInputHandler : NetworkBehaviour
    {
        [SerializeField] private InputActionReference _moveActionReference;
        [SerializeField] private InputActionReference _jumpActionReference;
        [SerializeField] private InputActionReference _lookActionReference;

        public event Action<Vector2> OnMovePerformed;
        public event Action<Vector2> OnMoveCancelled;
        public event Action<float> OnJump;

        public event Action<Vector2> OnLookPerformed;
        public event Action<Vector2> OnLookCancelled;

        /// <summary>
        /// Binding happens here rather than in OnEnable/OnDisable, and only for the owner.
        ///
        /// Both InputActionReferences resolve into one shared InputActionAsset, so every
        /// PlayerInputHandler in this process -- including the remote avatars the server also
        /// instantiates locally -- points at the same InputAction objects. Enable() and Disable()
        /// act on that shared object, not on this component, so a remote player spawning and
        /// disabling itself would switch off the local player's input as a side effect.
        /// OnNetworkSpawn is also the earliest point where IsOwner is actually known; OnEnable
        /// runs before the object has an owner.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            enabled = IsOwner;

            if (!IsOwner)
                return;

            _moveActionReference.action.Enable();
            _moveActionReference.action.performed += ProcessOnMove;
            _moveActionReference.action.canceled += ProcessOnMove;

            _jumpActionReference.action.Enable();
            _jumpActionReference.action.performed += ProcessOnJump;
            _jumpActionReference.action.canceled += ProcessOnJump;
            
            _lookActionReference.action.Enable();
            _lookActionReference.action.performed += ProcessOnLook;
            _lookActionReference.action.canceled += ProcessOnLook;
            
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner)
                return;

            _moveActionReference.action.performed -= ProcessOnMove;
            _moveActionReference.action.canceled -= ProcessOnMove;
            _moveActionReference.action.Disable();

            _jumpActionReference.action.performed -= ProcessOnJump;
            _jumpActionReference.action.canceled -= ProcessOnJump;
            _jumpActionReference.action.Disable();
            
            _lookActionReference.action.performed -= ProcessOnLook;
            _lookActionReference.action.canceled -= ProcessOnLook;
            _lookActionReference.action.Disable();
        }

        private void ProcessOnMove(InputAction.CallbackContext context)
        {
            Vector2 moveInput = context.ReadValue<Vector2>();

            switch (context.phase)
            {
                case InputActionPhase.Performed:
                    OnMovePerformed?.Invoke(moveInput);
                    break;
                case InputActionPhase.Canceled:
                    OnMoveCancelled?.Invoke(moveInput);
                    break;
            }
        }

        private void ProcessOnLook(InputAction.CallbackContext context)
        {
            Vector2 lookInput = context.ReadValue<Vector2>();

            switch (context.phase)
            {
                case InputActionPhase.Performed:
                    OnLookPerformed?.Invoke(lookInput);
                    break;
                case InputActionPhase.Canceled:
                    OnLookCancelled?.Invoke(lookInput);
                    break;
            }
        }

        private void ProcessOnJump(InputAction.CallbackContext context)
        {
            float jumpInput = context.ReadValue<float>();
            OnJump?.Invoke(jumpInput);
        }

    }
}
