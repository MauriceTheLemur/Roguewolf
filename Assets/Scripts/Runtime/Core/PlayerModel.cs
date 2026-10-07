using System;
using UnityEngine;

namespace Roguewolf
{
    public class PlayerModel
    {
        public PlayerRole Role { get; }
        public float MaxHealth { get; }
        public float Health { get; private set; }
        public float MaxStamina { get; }
        public float Stamina { get; private set; }
        public bool IsDead => Health <= 0f;

        public event Action<float> HealthChanged;
        public event Action<float> StaminaChanged;
        public event Action Died;

        public PlayerModel(PlayerConfig config, PlayerRole role)
        {
            Role = role;
            MaxHealth = config.MaxHealth;
            Health = MaxHealth;
            MaxStamina = config.MaxStamina;
            Stamina = MaxStamina;
        }

        public void TakeDamage(float amount) { /* clamp, fire HealthChanged, fire Died */ }

        public bool TryUseStamina(float amount)   // returns false if not enough
        {
            if (amount > Stamina) return false;
            Stamina -= amount;
            StaminaChanged?.Invoke(Stamina);
            return true;
        }

        public void RestoreStamina() { /* full refill when the player sleeps */ }
    
    }
}
