using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;

namespace Ashenfall.Data;

public sealed record OwnedItem(long Id, string ItemKey, string Rarity, int EnhanceLevel);

public sealed class ItemRepository
{
    private readonly Db _db;
    public ItemRepository(Db db) => _db = db;

    public async Task AddDropAsync(ulong steamId, string itemKey, string rarity)
    {
        await using var conn = await _db.OpenAsync();
        await conn.ExecuteAsync(
            "INSERT INTO item_instances (owner_steam_id, item_key, rarity) VALUES (@steamId, @itemKey, @rarity)",
            new { steamId, itemKey, rarity });
    }

    public async Task<IReadOnlyList<OwnedItem>> ListAsync(ulong steamId)
    {
        await using var conn = await _db.OpenAsync();
        var rows = await conn.QueryAsync<OwnedItem>(
            "SELECT id Id, item_key ItemKey, rarity Rarity, enhance_level EnhanceLevel FROM item_instances WHERE owner_steam_id = @steamId ORDER BY id DESC",
            new { steamId });
        return rows.ToList();
    }
}
