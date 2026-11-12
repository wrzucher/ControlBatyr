using ControlBatyr;

namespace Game.Model;

/// <summary>A player's bet on a game, including its current cancellation state.</summary>
/// <param name="Id">Identifies the bet for later lookup or cancellation.</param>
/// <param name="GameId">The game the player chose.</param>
/// <param name="PlayerId">The player who placed the bet.</param>
/// <param name="Amount">The amount the player bet.</param>
/// <param name="State">Whether the bet is still open or has been cancelled.</param>
/// <param name="GameStartsAt">The deadline for cancelling the bet.</param>
[ObjectModel]
public sealed record Bet(
    Guid Id, Guid GameId, Guid PlayerId, decimal Amount, BetState State, DateTimeOffset GameStartsAt)
{
    /// <summary>Whether the bet is still open.</summary>
    public bool IsOpen => State == BetState.Open;
}
