using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Roguewolf
{
    public enum RoleType
    {
        Werewolf,
        Villager,
        Seer,
        Hunter,
        Jester
    }

    [CreateAssetMenu(fileName = "PlayerRole", menuName = "Roguewolf/PlayerRole")]
    public class PlayerRole : ScriptableObject
    {
        public RoleType RoleType;
        public List<RoleAbility> Abilities = new List<RoleAbility>();
    }
}
