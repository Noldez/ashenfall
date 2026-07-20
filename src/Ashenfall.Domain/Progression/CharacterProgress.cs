namespace Ashenfall.Domain.Progression;

public readonly record struct CharacterProgress(int Level, long Xp)
{
    public CharacterProgress AddXp(long amount, out int levelsGained)
    {
        var level = Level;
        var xp = Xp + amount;
        levelsGained = 0;
        while (level < XpCurve.MaxLevel && xp >= XpCurve.XpForLevel(level))
        {
            xp -= XpCurve.XpForLevel(level);
            level++;
            levelsGained++;
        }
        if (level >= XpCurve.MaxLevel)
            xp = 0;
        return new CharacterProgress(level, xp);
    }
}
