using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ashenfall.Core.Sessions;
using Ashenfall.Data;
using Ashenfall.Domain.Combat;
using Ashenfall.Domain.Loot;
using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.HookParams;
using Sharp.Shared.Listeners;
using Sharp.Shared.Types;
using Sharp.Shared.Units;

namespace Ashenfall.Core.Mobs;

// Spawns configured mobs as prop_dynamic entities, tracks their health against an
// EntityDispatchTraceAttack pre-hook, and handles death (XP/gold/loot) and respawn.
//
// All public members and the hook callback run on the game thread (hook dispatch, timers,
// and server-command handlers are all main-thread in ModSharp), so a plain Dictionary keyed
// by EntityIndex is safe - no locking needed. The only asynchronous work is the loot
// persistence call, which is fired via Task.Run *after* every game-object access is done.
public sealed class MobManager : IEntityListener, IGameListener
{
    private sealed class ActiveMob
    {
        public required MobDefinition             Def    { get; init; }
        public required MobSpawnPoint             Point  { get; init; }
        public required int                       Health { get; set; }
        public required CEntityHandle<IBaseEntity> Handle { get; init; }
    }

    private readonly ISharedSystem _shared;
    private readonly SessionManager _sessions;
    private readonly ItemRepository _items;
    private readonly MobConfig _config;
    private readonly IReadOnlyDictionary<string, LootTable> _lootTables;
    private readonly Action<PlayerSession, long> _awardXp;
    private readonly ILogger _logger;
    private readonly Random _rng = new();
    private readonly Dictionary<EntityIndex, ActiveMob> _mobs = new();

    private bool _running;

    public MobManager(ISharedSystem shared,
        SessionManager sessions,
        ItemRepository items,
        MobConfig config,
        IReadOnlyDictionary<string, LootTable> lootTables,
        Action<PlayerSession, long> awardXp,
        ILogger logger)
    {
        _shared = shared;
        _sessions = sessions;
        _items = items;
        _config = config;
        _lootTables = lootTables;
        _awardXp = awardXp;
        _logger = logger;
    }

    public void Init()
    {
        _running = true;
        _shared.GetHookManager().EntityDispatchTraceAttack.InstallHookPre(OnEntityTraceAttack);
        _shared.GetEntityManager().InstallEntityListener(this);
        _shared.GetModSharp().InstallGameListener(this);
    }

    public void SpawnAll()
    {
        foreach (var point in _config.SpawnPoints)
            SpawnOne(point);
    }

    public void Clear()
    {
        _running = false;
        _shared.GetHookManager().EntityDispatchTraceAttack.RemoveHookPre(OnEntityTraceAttack);
        _shared.GetEntityManager().RemoveEntityListener(this);
        _shared.GetModSharp().RemoveGameListener(this);

        var entityManager = _shared.GetEntityManager();
        foreach (var index in _mobs.Keys)
            entityManager.FindEntityByIndex(index)?.Kill();

        _mobs.Clear();
    }

    // IEntityListener - prunes mobs the engine deletes out from under us (round restart,
    // map change, etc.) so stale EntityIndex keys never linger in _mobs.
    void IEntityListener.OnEntityDeleted(IBaseEntity entity)
    {
        _mobs.Remove(entity.Index);
    }

    int IEntityListener.ListenerVersion  => IEntityListener.ApiVersion;
    int IEntityListener.ListenerPriority => 0;

    // IGameListener - mob models must be precached at map load or props render as the
    // red ERROR placeholder (player models are not otherwise precached on every map).
    void IGameListener.OnResourcePrecache()
    {
        var modSharp = _shared.GetModSharp();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in _config.Mobs.Values)
        {
            if (seen.Add(def.Model))
                modSharp.PrecacheResource(def.Model);
        }
    }

    int IGameListener.ListenerVersion  => IGameListener.ApiVersion;
    int IGameListener.ListenerPriority => 0;

    private void SpawnOne(MobSpawnPoint point)
    {
        if (!_running) return;

        if (!_config.Mobs.TryGetValue(point.Mob, out var def))
        {
            _logger.LogWarning("Unknown mob key '{Mob}' referenced by a spawn point", point.Mob);
            return;
        }

        var keyValues = new Dictionary<string, KeyValuesVariantValueItem>
        {
            ["model"]  = def.Model,
            ["origin"] = $"{point.X} {point.Y} {point.Z}",
            ["solid"]  = 6, // SOLID_VPHYSICS
        };

        var entity = _shared.GetEntityManager().SpawnEntitySync("prop_dynamic", keyValues);
        if (entity is null)
        {
            _logger.LogWarning("Failed to spawn mob '{Mob}'", point.Mob);
            return;
        }

        // Player models do not reliably apply via the "model" keyvalue on prop_dynamic;
        // setting it again post-spawn makes the engine actually use it.
        entity.SetModel(def.Model);

        _mobs[entity.Index] = new ActiveMob
        {
            Def    = def,
            Point  = point,
            Health = def.MaxHealth,
            Handle = entity.Handle,
        };
    }

    private HookReturnValue<long> OnEntityTraceAttack(
        IEntityDispatchTraceAttackHookParams param,
        HookReturnValue<long> current)
    {
        if (!_mobs.TryGetValue(param.Entity.Index, out var mob))
            return new HookReturnValue<long>();

        // Belt-and-suspenders on top of the IEntityListener pruning above: if the index was
        // reused for a different entity before we heard about the deletion, the handle won't
        // match anymore - drop the stale entry and ignore the hit.
        if (mob.Handle != param.Entity.Handle)
        {
            _mobs.Remove(param.Entity.Index);
            return new HookReturnValue<long>();
        }

        var attacker = _shared.GetEntityManager().FindEntityByHandle(param.AttackerHandle);
        var client = attacker?.AsPlayerPawn()?.GetControllerAuto()?.GetGameClient()
            ?? attacker?.AsPlayerController()?.GetGameClient();
        var session = client is null ? null : _sessions.Get(client);

        if (client is not null && session is not null)
        {
            var result = DamageCalculator.PlayerToMob(param.Damage, session.Stats,
                param.HitGroup == HitGroupType.Head, _rng);
            var applied = (int)MathF.Round(result.Damage, MidpointRounding.AwayFromZero);
            mob.Health -= applied;

            client.Print(HudPrintChannel.Center,
                $"{applied}{(result.IsCrit ? " CRIT" : "")}");

            if (mob.Health <= 0)
                HandleDeath(param.Entity, mob, session);
        }

        // Mobs manage their own health entirely on our side - always skip the engine's
        // own damage-application path for entities we are tracking.
        return new HookReturnValue<long>(EHookAction.SkipCallReturnOverride);
    }

    private void HandleDeath(IBaseEntity entity, ActiveMob mob, PlayerSession session)
    {
        _mobs.Remove(entity.Index);
        entity.Kill();

        _awardXp(session, mob.Def.XpReward);
        session.Character.Gold += mob.Def.GoldReward;
        session.Client.Print(HudPrintChannel.Chat,
            $"[Ashenfall] {mob.Def.Name} slain! +{mob.Def.XpReward} XP, +{mob.Def.GoldReward} gold");

        if (_lootTables.TryGetValue(mob.Def.LootTable, out var table))
        {
            var drop = table.Roll(mob.Def.DropChance, _rng);
            if (drop is not null)
            {
                session.Client.Print(HudPrintChannel.Chat,
                    $"[Ashenfall] Loot: [{drop.Rarity}] {drop.ItemKey}");

                var steamId = session.Character.SteamId;
                var itemKey = drop.ItemKey;
                var rarity = drop.Rarity.ToString();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _items.AddDropAsync(steamId, itemKey, rarity);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Failed to persist loot drop for {SteamId}", steamId);
                    }
                });
            }
        }

        var point = mob.Point;
        _shared.GetModSharp().PushTimer(() => SpawnOne(point), point.RespawnSeconds,
            GameTimerFlags.StopOnMapEnd);
    }
}
