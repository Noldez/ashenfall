using System;

namespace Ashenfall.Domain.Progression;

public static class XpCurve
{
    public const int MaxLevel = 60;

    // 100 XP at level 1, +12% per level, rounded to nearest 10.
    public static long XpForLevel(int level)
    {
        if (level < 1 || level >= MaxLevel + 1)
            throw new ArgumentOutOfRangeException(nameof(level));
        var raw = 100.0 * Math.Pow(1.12, level - 1);
        return (long)(Math.Round(raw / 10.0) * 10.0);
    }
}
