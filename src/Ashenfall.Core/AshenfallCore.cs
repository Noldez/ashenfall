using System;
using Microsoft.Extensions.Configuration;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;

namespace Ashenfall.Core;

public sealed class AshenfallCore : IModSharpModule
{
    private readonly ISharedSystem _shared;

    public AshenfallCore(ISharedSystem sharedSystem,
        string dllPath,
        string sharpPath,
        Version version,
        IConfiguration coreConfiguration,
        bool hotReload)
        => _shared = sharedSystem;

    public bool Init()
    {
        _shared.GetClientManager().InstallCommandCallback("rpg", OnRpgCommand);
        return true;
    }

    public void Shutdown()
    {
        _shared.GetClientManager().RemoveCommandCallback("rpg", OnRpgCommand);
    }

    private ECommandAction OnRpgCommand(IGameClient client, StringCommand command)
    {
        client.Print(HudPrintChannel.Chat, "[Ashenfall] It lives. RPG menu coming soon.");
        return ECommandAction.Stopped;
    }

    public string DisplayName   => "Ashenfall Core";
    public string DisplayAuthor => "Noldez";
}
