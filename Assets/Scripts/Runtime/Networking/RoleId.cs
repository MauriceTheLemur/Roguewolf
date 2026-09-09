namespace Roguewolf.Networking
{
    /// <summary>
    /// Placeholder role set. Roguelike role/trait variety gets added here later -- what matters
    /// for the netcode layer is that this is an unmanaged type, so it can ride inside a
    /// <c>NetworkVariable</c> with owner-only read permission.
    /// </summary>
    public enum RoleId : byte
    {
        None = 0,
        Villager = 1,
        Werewolf = 2,
        Seer = 3
    }

    public enum RoleTeam : byte
    {
        Unknown = 0,
        Village = 1,
        Wolves = 2
    }

    public static class RoleIdExtensions
    {
        public static RoleTeam Team(this RoleId role) => role switch
        {
            RoleId.Villager => RoleTeam.Village,
            RoleId.Seer => RoleTeam.Village,
            RoleId.Werewolf => RoleTeam.Wolves,
            _ => RoleTeam.Unknown
        };
    }
}
