namespace Game.Model;

/// <summary>The recorded bet announced to listeners.</summary>
/// <param name="Bet">The bet as recorded when it was placed.</param>
public sealed record BetPlacedEventArgs(Bet Bet);
