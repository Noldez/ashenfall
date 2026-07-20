using System;
using Ashenfall.Domain.Combat;
using Xunit;

public class DamageCalculatorTests
{
    private static readonly PlayerCombatStats Base = new(1.0, 0.0, 2.0);

    [Fact]
    public void No_modifiers_passes_damage_through()
        => Assert.Equal(30f, DamageCalculator.PlayerToMob(30f, Base, false, new Random(1)).Damage);

    [Fact]
    public void Headshot_applies_4x()
        => Assert.Equal(120f, DamageCalculator.PlayerToMob(30f, Base, true, new Random(1)).Damage);

    [Fact]
    public void Crit_chance_1_always_crits_and_multiplies()
    {
        var stats = new PlayerCombatStats(1.0, 1.0, 2.0);
        var result = DamageCalculator.PlayerToMob(30f, stats, false, new Random(1));
        Assert.True(result.IsCrit);
        Assert.Equal(60f, result.Damage);
    }

    [Fact]
    public void Gear_multiplier_scales_damage()
        => Assert.Equal(45f, DamageCalculator.PlayerToMob(30f, new PlayerCombatStats(1.5, 0.0, 2.0), false, new Random(1)).Damage);
}
