using Ashenfall.Data;
using Ashenfall.Domain.Combat;
using Sharp.Shared.Objects;

namespace Ashenfall.Core.Sessions;

public sealed class PlayerSession
{
    public required IGameClient Client { get; init; }
    public required CharacterRecord Character { get; init; }

    // Marksman baseline; per-level growth: +1% damage per level.
    public PlayerCombatStats Stats =>
        new(1.0 + 0.01 * (Character.Level - 1), 0.05, 2.0);
}
