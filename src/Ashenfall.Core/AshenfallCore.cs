using System;
using System.IO;
using System.Text.Json;
using Ashenfall.Core.Mobs;
using Ashenfall.Core.Sessions;
using Ashenfall.Core.Ui;
using Ashenfall.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sharp.Modules.MenuManager.Shared;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;

namespace Ashenfall.Core;

public sealed class AshenfallCore : IModSharpModule
{
    private const string MenuManagerAssemblyName = "Sharp.Modules.MenuManager";

    private const string SpawnMobsCommandName = "ash_spawnmobs";

    private readonly ISharedSystem _shared;
    private readonly CharacterRepository _characters;
    private readonly ItemRepository _items;
    private readonly SessionManager _sessions;
    private readonly MobManager _mobs;

    private IModSharpModuleInterface<IMenuManager>? _menuManager;

    private Guid _autosaveTimer;

    public AshenfallCore(ISharedSystem sharedSystem,
        string dllPath,
        string sharpPath,
        Version version,
        IConfiguration coreConfiguration,
        bool hotReload)
    {
        _shared = sharedSystem;

        var cfgPath = Path.Combine(sharpPath, "configs", "ashenfall", "db.secrets.json");
        var doc = JsonDocument.Parse(File.ReadAllText(cfgPath));
        var connStr = doc.RootElement.GetProperty("ConnectionString").GetString()!;
        var db = new Db(connStr);
        _characters = new CharacterRepository(db);
        _items = new ItemRepository(db);
        _sessions = new SessionManager(_shared, _characters,
            _shared.GetLoggerFactory().CreateLogger<SessionManager>());

        var mobConfig = MobConfig.Load(Path.Combine(sharpPath, "configs", "ashenfall", "mobs.json"));
        var lootTables = LootConfigLoader.Load(Path.Combine(sharpPath, "configs", "ashenfall", "loot.json"));
        _mobs = new MobManager(_shared, _sessions, _items, mobConfig, lootTables, AwardXp,
            _shared.GetLoggerFactory().CreateLogger<MobManager>());
    }

    public SessionManager Sessions => _sessions;

    public bool Init()
    {
        _shared.GetClientManager().InstallCommandCallback("rpg", OnRpgCommand);
        _shared.GetClientManager().InstallClientListener(_sessions);
        _autosaveTimer = _shared.GetModSharp().PushTimer(_sessions.SaveAll, 30.0, GameTimerFlags.Repeatable);

        _mobs.Init();
        _shared.GetConVarManager().CreateServerCommand(SpawnMobsCommandName, OnSpawnMobsCommand,
            "Spawn Ashenfall mobs", ConVarFlags.Release);

        return true;
    }

    public void PostInit()
    {
        // Also resolve here in case our module is hot-reloaded after initial startup,
        // since OnAllModulesLoaded is only called once at first load.
        TryResolveMenuManager();
    }

    public void OnLibraryConnected(string name)
    {
        if (name.Equals(MenuManagerAssemblyName, StringComparison.OrdinalIgnoreCase))
            TryResolveMenuManager();
    }

    public void OnAllModulesLoaded()
    {
        TryResolveMenuManager();
    }

    public void Shutdown()
    {
        _shared.GetConVarManager().ReleaseCommand(SpawnMobsCommandName);
        _mobs.Clear();

        _shared.GetClientManager().RemoveCommandCallback("rpg", OnRpgCommand);
        _shared.GetClientManager().RemoveClientListener(_sessions);
        _shared.GetModSharp().StopTimer(_autosaveTimer);
        _sessions.SaveAll();
    }

    public void AwardXp(Sessions.PlayerSession s, long amount)
    {
        var before = s.Character.Level;
        var progress = new Ashenfall.Domain.Progression.CharacterProgress(s.Character.Level, s.Character.Xp)
            .AddXp(amount, out var gained);
        s.Character.Level = progress.Level;
        s.Character.Xp = progress.Xp;
        if (gained > 0)
            s.Client.Print(HudPrintChannel.Chat,
                $"[Ashenfall] LEVEL UP! {before} -> {progress.Level}");
    }

    private ECommandAction OnRpgCommand(IGameClient client, StringCommand command)
    {
        var session = _sessions.Get(client);
        if (session is null)
        {
            client.Print(HudPrintChannel.Chat, "[Ashenfall] Character still loading, try again shortly.");
            return ECommandAction.Stopped;
        }

        if (_menuManager?.Instance is { } menus)
        {
            RpgMenu.Show(client, session, menus);
        }
        else
        {
            var c = session.Character;
            var toNext = c.Level >= Ashenfall.Domain.Progression.XpCurve.MaxLevel
                ? 0
                : Ashenfall.Domain.Progression.XpCurve.XpForLevel(c.Level) - c.Xp;
            client.Print(HudPrintChannel.Chat, $"[Ashenfall] {c.Name} - Level {c.Level} {c.Class}");
            client.Print(HudPrintChannel.Chat,
                c.Level >= Ashenfall.Domain.Progression.XpCurve.MaxLevel
                    ? "[Ashenfall] MAX LEVEL"
                    : $"[Ashenfall] XP to next level: {toNext}");
            client.Print(HudPrintChannel.Chat, $"[Ashenfall] Gold: {c.Gold}");
        }

        return ECommandAction.Stopped;
    }

    private ECommandAction OnSpawnMobsCommand(StringCommand command)
    {
        _mobs.SpawnAll();
        return ECommandAction.Stopped;
    }

    private void TryResolveMenuManager()
    {
        // Re-resolve if the wrapper is null or the instance was disposed (e.g. after hot-reload)
        if (_menuManager?.Instance is not null)
            return;

        _menuManager = _shared.GetSharpModuleManager()
            .GetOptionalSharpModuleInterface<IMenuManager>(IMenuManager.Identity);
    }

    public string DisplayName   => "Ashenfall Core";
    public string DisplayAuthor => "Noldez";
}
