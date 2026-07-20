using Ashenfall.Core.Sessions;
using Ashenfall.Domain.Progression;
using Sharp.Modules.MenuManager.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.Objects;

namespace Ashenfall.Core.Ui;

public static class RpgMenu
{
    public static void Show(IGameClient client, PlayerSession session, IMenuManager menus)
    {
        var c = session.Character;
        var toNext = c.Level >= XpCurve.MaxLevel ? 0 : XpCurve.XpForLevel(c.Level) - c.Xp;
        var menu = Menu.Create()
            .Title("Ashenfall")
            .DisabledItem($"{c.Name} - Level {c.Level} {c.Class}")
            .DisabledItem(c.Level >= XpCurve.MaxLevel ? "MAX LEVEL" : $"XP to next level: {toNext}")
            .DisabledItem($"Gold: {c.Gold}")
            .Item("Inventory", ctrl =>
            {
                ctrl.Client.Print(HudPrintChannel.Chat, "[Ashenfall] Inventory opens in Task 14.");
                ctrl.Exit();
            })
            .ExitItem()
            .Build();
        menus.DisplayMenu(client, menu);
    }
}
