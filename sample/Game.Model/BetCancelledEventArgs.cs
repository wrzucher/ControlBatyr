namespace Game.Model;

/// <summary>Identifies the bet whose cancellation has been recorded.</summary>
/// <param name="BetId">The cancelled bet, for lookup or related updates.</param>
public sealed record BetCancelledEventArgs(Guid BetId);
