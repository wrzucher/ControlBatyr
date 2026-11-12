namespace Game.Model;

/// <summary>Identifies the bet whose cancellation has been recorded.</summary>
/// <param name="BetId">The cancelled bet, for lookup or related updates.</param>
/// <param name="PlayerId">The player who receives the refunded bet amount.</param>
/// <param name="Amount">The bet amount to return.</param>
public sealed record BetCancelledEventArgs(Guid BetId, Guid PlayerId, decimal Amount);
