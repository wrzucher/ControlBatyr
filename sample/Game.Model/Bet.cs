using ControlBatyr;

namespace Game.Model;

/// <summary>A player's stake on a game, including its current cancellation state.</summary>
/// <param name="Id">Identifies the bet for later lookup or cancellation.</param>
/// <param name="GameId">The game the player chose.</param>
/// <param name="PlayerId">The player who owns the stake.</param>
/// <param name="Amount">The amount the player committed.</param>
/// <param name="State">Whether the stake is still in play or has been withdrawn.</param>
/// <param name="GameStartsAt">The deadline for withdrawing the stake.</param>
[ObjectModel]
public sealed record Bet(
    Guid Id, Guid GameId, Guid PlayerId, decimal Amount, BetState State, DateTimeOffset GameStartsAt)
{
    /// <summary>Whether cancellation is still possible before the game starts.</summary>
    public bool IsOpen => State == BetState.Open;
}
