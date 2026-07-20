using System;
using System.Threading.Tasks;
using Ashenfall.Data;
using Xunit;

public class CharacterRepositoryTests
{
    private static string? ConnStr => Environment.GetEnvironmentVariable("ASHENFALL_TEST_DB");

    [SkippableFact]
    public async Task Create_load_save_roundtrip()
    {
        Skip.If(ConnStr is null, "ASHENFALL_TEST_DB not set");
        var repo = new CharacterRepository(new Db(ConnStr!));
        var steamId = (ulong)Random.Shared.NextInt64(1_000_000, long.MaxValue);
        var created = await repo.CreateAsync(steamId, "TestHero", "Marksman");
        Assert.Equal(1, created.Level);
        created.Level = 5;
        created.Gold = 123;
        created.Class = "Vanguard";
        await repo.SaveAsync(created);
        var loaded = await repo.LoadAsync(steamId);
        Assert.Equal(5, loaded!.Level);
        Assert.Equal(123, loaded.Gold);
        Assert.Equal("Vanguard", loaded.Class);
    }
}
