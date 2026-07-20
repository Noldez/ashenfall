using System;
using System.Collections.Generic;
using Ashenfall.Domain.Loot;
using Xunit;

public class LootTableTests
{
    private static LootTable Table() => new(new List<LootEntry>
    {
        new("rusty_pistol", Rarity.Common, 90),
        new("soldier_rifle", Rarity.Rare, 10),
    });

    [Fact]
    public void Roll_returns_null_when_drop_chance_misses()
        => Assert.Null(Table().Roll(0.0, new Random(1)));

    [Fact]
    public void Roll_returns_entry_when_chance_hits()
        => Assert.NotNull(Table().Roll(1.0, new Random(1)));

    [Fact]
    public void Weights_are_respected_within_tolerance()
    {
        var rng = new Random(42);
        int rare = 0;
        for (var i = 0; i < 10_000; i++)
            if (Table().Roll(1.0, rng)!.Rarity == Rarity.Rare) rare++;
        Assert.InRange(rare, 800, 1200); // 10% weight
    }

    [Fact]
    public void Every_rarity_has_a_hex_color()
    {
        foreach (Rarity r in Enum.GetValues<Rarity>())
            Assert.Matches("^#[0-9A-F]{6}$", RarityColors.Hex(r));
    }
}
