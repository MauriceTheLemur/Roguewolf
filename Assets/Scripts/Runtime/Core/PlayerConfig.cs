using UnityEngine;

namespace Roguewolf
{
    /// <summary>
    /// Base stats every player starts a match with. Read-only at runtime --
    /// PlayerModel copies these values in, so upgrades never modify this asset.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "Roguewolf/Player Config")]
    public class PlayerConfig : ScriptableObject
    {
        [SerializeField, Min(1f)] private float _maxHealth = 100f;
        [SerializeField, Min(0f)] private float _maxStamina = 100f;

        public float MaxHealth => _maxHealth;
        public float MaxStamina => _maxStamina;
    }
}
