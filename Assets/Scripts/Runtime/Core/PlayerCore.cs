using System;
using UnityEngine;

namespace Roguewolf
{
    public class PlayerCore : MonoBehaviour
    {
        [SerializeField] private PlayerConfig _config;
        [SerializeField] private PlayerRole _role;

        public event Action OnInitialized;
        public bool IsInitialized { get; private set; }
        
        private PlayerModel _model;

        private void Awake()
        {
            IsInitialized = false;
        }

        private void OnEnable()
        {
            Initialize();
        }

        private void Initialize()
        {
            _model = new PlayerModel(_config, _role);
            OnInitialized?.Invoke();
            IsInitialized = true;
            
        } 
    }
}
