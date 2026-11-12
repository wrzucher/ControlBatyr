namespace Game.Model;

/// <summary>Identifies the bet whose cancellation has been recorded.</summary>
/// <param name="BetId">The cancelled bet, for lookup or related updates.</param>
/// <param name="PlayerId">The player whose stake is returned.</param>
/// <param name="Amount">The stake to return.</param>
public sealed record BetCancelledEventArgs(Guid BetId, Guid PlayerId, decimal Amount);
