using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Ashenfall.Domain.Loot;

namespace Ashenfall.Core.Mobs;

public sealed record MobDefinition(string Key, string Name, string Model,
    int MaxHealth, long XpReward, long GoldReward, double DropChance, string LootTable);

public sealed record MobSpawnPoint(string Mob, float X, float Y, float Z, int RespawnSeconds);

public sealed class MobConfig
{
    public required IReadOnlyDictionary<string, MobDefinition> Mobs { get; init; }
    public required IReadOnlyList<MobSpawnPoint> SpawnPoints { get; init; }

    public static MobConfig Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var mobs = new Dictionary<string, MobDefinition>();
        foreach (var m in doc.RootElement.GetProperty("mobs").EnumerateArray())
        {
            var def = new MobDefinition(
                m.GetProperty("key").GetString()!,
                m.GetProperty("name").GetString()!,
                m.GetProperty("model").GetString()!,
                m.GetProperty("maxHealth").GetInt32(),
                m.GetProperty("xpReward").GetInt64(),
                m.GetProperty("goldReward").GetInt64(),
                m.GetProperty("dropChance").GetDouble(),
                m.GetProperty("lootTable").GetString()!);
            mobs[def.Key] = def;
        }
        var points = new List<MobSpawnPoint>();
        foreach (var p in doc.RootElement.GetProperty("spawnPoints").EnumerateArray())
            points.Add(new MobSpawnPoint(
                p.GetProperty("mob").GetString()!,
                p.GetProperty("x").GetSingle(),
                p.GetProperty("y").GetSingle(),
                p.GetProperty("z").GetSingle(),
                p.GetProperty("respawnSeconds").GetInt32()));
        return new MobConfig { Mobs = mobs, SpawnPoints = points };
    }
}

// Loads the named loot tables (Task 8-9's LootTable/LootEntry domain types) from loot.json.
public static class LootConfigLoader
{
    public static IReadOnlyDictionary<string, LootTable> Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var tables = new Dictionary<string, LootTable>();
        foreach (var table in doc.RootElement.GetProperty("tables").EnumerateObject())
        {
            var entries = new List<LootEntry>();
            foreach (var e in table.Value.EnumerateArray())
            {
                entries.Add(new LootEntry(
                    e.GetProperty("itemKey").GetString()!,
                    Enum.Parse<Rarity>(e.GetProperty("rarity").GetString()!),
                    e.GetProperty("weight").GetDouble()));
            }
            tables[table.Name] = new LootTable(entries);
        }
        return tables;
    }
}
