using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

    // Tracks the in-flight disconnect-save Task for a steamId so a fast reconnect can wait
    // for the prior save to finish before loading - otherwise the load could race the save
    // and read stale data.
    private readonly ConcurrentDictionary<ulong, Task> _pendingDisconnectSaves = new();

    public SessionManager(ISharedSystem shared, CharacterRepository characters, ILogger logger)
    {
        _shared = shared;
        _characters = characters;
        _logger = logger;
    }

    public PlayerSession? Get(IGameClient client)
        => _sessions.TryGetValue(client.SteamId.AsPrimitive(), out var s) ? s : null;

    // Collects every session's save Task so callers can await completion (e.g. a bounded
    // wait at shutdown). The 30s autosave timer callback is free to discard the returned
    // Task - failures are still logged inside SaveQuietAsync.
    public Task SaveAll()
    {
        var tasks = new List<Task>(_sessions.Count);
        foreach (var s in _sessions.Values)
            tasks.Add(SaveQuietAsync(CloneRecord(s.Character)));

        return Task.WhenAll(tasks);
    }

    // Game-thread only - clones the record before handing it to the thread pool so a save
    // never observes a torn snapshot of fields being mutated concurrently on the main thread.
    private static CharacterRecord CloneRecord(CharacterRecord c) => new()
    {
        SteamId = c.SteamId,
        Name    = c.Name,
        Class   = c.Class,
        Level   = c.Level,
        Xp      = c.Xp,
        Gold    = c.Gold,
    };

    private async Task SaveQuietAsync(CharacterRecord snapshot)
    {
        try { await _characters.SaveAsync(snapshot); }
        catch (Exception e) { _logger.LogError(e, "Save failed for {SteamId}", snapshot.SteamId); }
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
                // A fast reconnect can race a still-in-flight disconnect save for the same
                // steamId - wait for it so we never load data that is about to be overwritten.
                if (_pendingDisconnectSaves.TryGetValue(steamId, out var pending))
                {
                    try { await pending; }
                    catch { /* already logged by SaveQuietAsync */ }
                }

                var character = await _characters.LoadAsync(steamId)
                                ?? await _characters.CreateAsync(steamId, name, "Marksman");

                // Marshal back to the main thread before touching IGameClient or _sessions -
                // the player may have disconnected while the DB call was in flight.
                _shared.GetModSharp().InvokeAction(() =>
                {
                    if (!client.IsValid) return;
                    if (client.SteamId.AsPrimitive() != steamId) return;

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

        var steamId = client.SteamId.AsPrimitive();
        if (!_sessions.TryRemove(steamId, out var s)) return;

        var snapshot = CloneRecord(s.Character);
        var task = SaveQuietAsync(snapshot);
        _pendingDisconnectSaves[steamId] = task;

        _ = task.ContinueWith(t =>
        {
            ((ICollection<KeyValuePair<ulong, Task>>)_pendingDisconnectSaves)
                .Remove(new KeyValuePair<ulong, Task>(steamId, t));
        }, TaskScheduler.Default);
    }

    int IClientListener.ListenerVersion => IClientListener.ApiVersion;
    int IClientListener.ListenerPriority => 0;
}
