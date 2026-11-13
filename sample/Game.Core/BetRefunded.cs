using ControlBatyr;

namespace Game.Core;

/// <summary>A cancelled bet amount was returned to the player.</summary>
[Event<AccountManager>]
public sealed record BetRefunded(Guid PlayerId, Guid BetId, decimal Amount);
