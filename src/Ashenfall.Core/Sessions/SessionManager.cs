using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Ashenfall.Data;
using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.Listeners;
using Sharp.Shared.Objects;

namespace Ashenfall.Core.Sessions;

public sealed class SessionManager : IClientListener
{
    private readonly ISharedSystem _shared;
    private readonly CharacterRepository _characters;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<ulong, PlayerSession> _sessions = new();

    public SessionManager(ISharedSystem shared, CharacterRepository characters, ILogger logger)
    {
        _shared = shared;
        _characters = characters;
        _logger = logger;
    }

    public PlayerSession? Get(IGameClient client)
        => _sessions.TryGetValue(client.SteamId.AsPrimitive(), out var s) ? s : null;

    public void SaveAll()
    {
        foreach (var s in _sessions.Values)
            _ = SaveQuietAsync(s);
    }

    private async Task SaveQuietAsync(PlayerSession s)
    {
        try { await _characters.SaveAsync(s.Character); }
        catch (Exception e) { _logger.LogError(e, "Save failed for {SteamId}", s.Character.SteamId); }
    }

    public void OnClientPutInServer(IGameClient client)
    {
        if (client.IsFakeClient) return;

        var steamId = client.SteamId.AsPrimitive();
        var name = client.Name;

        _ = Task.Run(async () =>
        {
            try
            {
                var character = await _characters.LoadAsync(steamId)
                                ?? await _characters.CreateAsync(steamId, name, "Marksman");

                // Marshal back to the main thread before touching IGameClient or _sessions -
                // the player may have disconnected while the DB call was in flight.
                _shared.GetModSharp().InvokeAction(() =>
                {
                    if (!client.IsValid) return;

                    _sessions[steamId] = new PlayerSession { Client = client, Character = character };
                    client.Print(HudPrintChannel.Chat,
                        $"[Ashenfall] Sveikas, {character.Name}! Level {character.Level} {character.Class}. Type !rpg");
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Load failed for {SteamId}", steamId);
                _shared.GetModSharp().InvokeAction(() =>
                {
                    if (!client.IsValid) return;
                    client.Print(HudPrintChannel.Chat, "[Ashenfall] Character load failed, safe mode.");
                });
            }
        });
    }

    public void OnClientDisconnected(IGameClient client, NetworkDisconnectionReason reason)
    {
        if (client.IsFakeClient) return;

        if (_sessions.TryRemove(client.SteamId.AsPrimitive(), out var s))
            _ = SaveQuietAsync(s);
    }

    int IClientListener.ListenerVersion => IClientListener.ApiVersion;
    int IClientListener.ListenerPriority => 0;
}
