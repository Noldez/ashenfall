using System;

namespace Ashenfall.Domain.Loot;

public enum Rarity { Common, Uncommon, Rare, Epic, Legendary, Mythic }

public static class RarityColors
{
    public static string Hex(Rarity r) => r switch
    {
        Rarity.Common    => "#FFFFFF",
        Rarity.Uncommon  => "#1EFF00",
        Rarity.Rare      => "#0070DD",
        Rarity.Epic      => "#A335EE",
        Rarity.Legendary => "#FF8000",
        Rarity.Mythic    => "#E6242B",
        _ => throw new ArgumentOutOfRangeException(nameof(r)),
    };
}
