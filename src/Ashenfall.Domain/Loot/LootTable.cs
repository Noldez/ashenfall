using System;
using System.Collections.Generic;
using System.Linq;

namespace Ashenfall.Domain.Loot;

public sealed record LootEntry(string ItemKey, Rarity Rarity, double Weight);

public sealed class LootTable
{
    private readonly IReadOnlyList<LootEntry> _entries;
    private readonly double _totalWeight;

    public LootTable(IReadOnlyList<LootEntry> entries)
    {
        if (entries.Count == 0) throw new ArgumentException("empty loot table");
        _entries = entries;
        _totalWeight = entries.Sum(e => e.Weight);
    }

    public LootEntry? Roll(double dropChance, Random rng)
    {
        if (rng.NextDouble() >= dropChance) return null;
        var roll = rng.NextDouble() * _totalWeight;
        foreach (var e in _entries)
        {
            roll -= e.Weight;
            if (roll <= 0) return e;
        }
        return _entries[^1];
    }
}
