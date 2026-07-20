using Ashenfall.Domain.Progression;
using Xunit;

public class XpCurveTests
{
    [Fact]
    public void Level1_needs_100_xp_to_reach_level2()
        => Assert.Equal(100, XpCurve.XpForLevel(1));

    [Fact]
    public void Curve_is_strictly_increasing_up_to_cap()
    {
        for (var lv = 1; lv < XpCurve.MaxLevel; lv++)
            Assert.True(XpCurve.XpForLevel(lv + 1) > XpCurve.XpForLevel(lv));
    }

    [Fact]
    public void AddXp_carries_overflow_across_levels()
    {
        var p = new CharacterProgress(1, 0);
        var next = p.AddXp(XpCurve.XpForLevel(1) + 10, out var gained);
        Assert.Equal(2, next.Level);
        Assert.Equal(10, next.Xp);
        Assert.Equal(1, gained);
    }

    [Fact]
    public void AddXp_stops_at_max_level()
    {
        var p = new CharacterProgress(XpCurve.MaxLevel, 0);
        var next = p.AddXp(1_000_000, out var gained);
        Assert.Equal(XpCurve.MaxLevel, next.Level);
        Assert.Equal(0, gained);
    }
}
