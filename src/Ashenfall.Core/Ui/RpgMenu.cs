using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ashenfall.Core.Sessions;
using Ashenfall.Data;
using Ashenfall.Domain.Loot;
using Ashenfall.Domain.Progression;
using Microsoft.Extensions.Logging;
using Sharp.Modules.MenuManager.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.Objects;

namespace Ashenfall.Core.Ui;

public static class RpgMenu
{
    public static void Show(IGameClient client, PlayerSession session, IMenuManager menus, ItemRepository items,
        Action<Action> mainThreadInvoke, ILogger logger)
    {
        var c = session.Character;
        var steamId = c.SteamId;
        var toNext = c.Level >= XpCurve.MaxLevel ? 0 : XpCurve.XpForLevel(c.Level) - c.Xp;
        var menu = Menu.Create()
            .Title("Ashenfall")
            .DisabledItem($"{c.Name} - Level {c.Level} {c.Class}")
            .DisabledItem(c.Level >= XpCurve.MaxLevel ? "MAX LEVEL" : $"XP to next level: {toNext}")
            .DisabledItem($"Gold: {c.Gold}")
            .Item("Inventory", ctrl =>
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var owned = await items.ListAsync(steamId);

                        // Marshal back to the main thread before touching IGameClient or the menu manager -
                        // the player may have disconnected or closed the menu while the DB call was in flight.
                        mainThreadInvoke(() =>
                        {
                            if (!client.IsValid) return;

                            var inv = BuildInventoryMenu(owned, client, session, menus, items, mainThreadInvoke, logger);
                            menus.DisplayMenu(client, inv);
                        });
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Failed to load inventory for {SteamId}", steamId);
                        mainThreadInvoke(() =>
                        {
                            if (!client.IsValid) return;
                            client.Print(HudPrintChannel.Chat, "[Ashenfall] Failed to load inventory, try again shortly.");
                        });
                    }
                });
            })
            .ExitItem()
            .Build();
        menus.DisplayMenu(client, menu);
    }

    private static Menu BuildInventoryMenu(IReadOnlyList<OwnedItem> owned, IGameClient client, PlayerSession session,
        IMenuManager menus, ItemRepository items, Action<Action> mainThreadInvoke, ILogger logger)
    {
        var builder = Menu.Create().Title($"Inventory ({owned.Count})");

        if (owned.Count == 0)
        {
            builder.DisabledItem("Empty. Go kill something.");
        }
        else
        {
            foreach (var item in owned)
            {
                var label = item.EnhanceLevel > 0
                    ? $"[{item.Rarity}] {item.ItemKey} +{item.EnhanceLevel}"
                    : $"[{item.Rarity}] {item.ItemKey}";
                var color = Enum.TryParse<Rarity>(item.Rarity, out var rarity) ? RarityColors.Hex(rarity) : null;

                builder.Item((IGameClient _, ref MenuItemContext ctx) =>
                {
                    ctx.Title = label;
                    ctx.Color = color;
                    // Rows need an action or the renderer treats them as disabled, which
                    // suppresses the Color override above - a no-op keeps rarity colors visible.
                    ctx.Action = _ => { };
                });
            }
        }

        // DisplayMenu() always starts a fresh menu session for the client (see MenuManager.DisplayMenu),
        // so there is no parent to GoBack() to here. Re-display the main menu instead.
        builder.Item("Back", _ => Show(client, session, menus, items, mainThreadInvoke, logger));

        return builder.Build();
    }
}
