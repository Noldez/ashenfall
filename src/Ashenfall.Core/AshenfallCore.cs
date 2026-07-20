using System;
using System.IO;
using System.Text.Json;
using Ashenfall.Core.Sessions;
using Ashenfall.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;

namespace Ashenfall.Core;

public sealed class AshenfallCore : IModSharpModule
{
    private readonly ISharedSystem _shared;
    private readonly CharacterRepository _characters;
    private readonly SessionManager _sessions;

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
        _characters = new CharacterRepository(new Db(connStr));
        _sessions = new SessionManager(_shared, _characters,
            _shared.GetLoggerFactory().CreateLogger<SessionManager>());
    }

    public SessionManager Sessions => _sessions;

    public bool Init()
    {
        _shared.GetClientManager().InstallCommandCallback("rpg", OnRpgCommand);
        _shared.GetClientManager().InstallClientListener(_sessions);
        _autosaveTimer = _shared.GetModSharp().PushTimer(_sessions.SaveAll, 30.0, GameTimerFlags.Repeatable);
        return true;
    }

    public void Shutdown()
    {
        _shared.GetClientManager().RemoveCommandCallback("rpg", OnRpgCommand);
        _shared.GetClientManager().RemoveClientListener(_sessions);
        _shared.GetModSharp().StopTimer(_autosaveTimer);
        _sessions.SaveAll();
    }

    private ECommandAction OnRpgCommand(IGameClient client, StringCommand command)
    {
        client.Print(HudPrintChannel.Chat, "[Ashenfall] It lives. RPG menu coming soon.");
        return ECommandAction.Stopped;
    }

    public string DisplayName   => "Ashenfall Core";
    public string DisplayAuthor => "Noldez";
}
