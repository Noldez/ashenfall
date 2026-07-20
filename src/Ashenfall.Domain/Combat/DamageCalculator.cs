using System;

namespace Ashenfall.Domain.Combat;

public readonly record struct PlayerCombatStats(double DamageMultiplier, double CritChance, double CritMultiplier);

public readonly record struct DamageResult(float Damage, bool IsCrit);

public static class DamageCalculator
{
    public const float HeadshotMultiplier = 4.0f;

    public static DamageResult PlayerToMob(float baseDamage, PlayerCombatStats stats, bool isHeadshot, Random rng)
    {
        var dmg = baseDamage * (float)stats.DamageMultiplier;
        if (isHeadshot) dmg *= HeadshotMultiplier;
        var crit = rng.NextDouble() < stats.CritChance;
        if (crit) dmg *= (float)stats.CritMultiplier;
        return new DamageResult(dmg, crit);
    }
}
