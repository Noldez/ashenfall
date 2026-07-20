using System.Threading.Tasks;
using Dapper;

namespace Ashenfall.Data;

public sealed class CharacterRepository
{
    private readonly Db _db;
    public CharacterRepository(Db db) => _db = db;

    public async Task<CharacterRecord?> LoadAsync(ulong steamId)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QuerySingleOrDefaultAsync<CharacterRecord>(
            "SELECT steam_id SteamId, name Name, class Class, level Level, xp Xp, gold Gold FROM characters WHERE steam_id = @steamId",
            new { steamId });
    }

    public async Task<CharacterRecord> CreateAsync(ulong steamId, string name, string @class)
    {
        await using var conn = await _db.OpenAsync();
        await conn.ExecuteAsync(
            "INSERT INTO characters (steam_id, name, class) VALUES (@steamId, @name, @class)",
            new { steamId, name, @class });
        return (await LoadAsync(steamId))!;
    }

    public async Task SaveAsync(CharacterRecord c)
    {
        await using var conn = await _db.OpenAsync();
        await conn.ExecuteAsync(
            "UPDATE characters SET name = @Name, class = @Class, level = @Level, xp = @Xp, gold = @Gold WHERE steam_id = @SteamId", c);
    }
}
