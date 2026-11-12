using ControlBatyr;
using Game.Model;

namespace Game.Core;

/// <summary>Announces changes to player bets after they have been recorded.</summary>
[EventPublisher]
public sealed class GameEventPublisher
{
    /// <summary>The player's bet has been placed.</summary>
    public event Action<BetPlacedEventArgs>? BetPlaced;

    /// <summary>The player's bet has been cancelled.</summary>
    public event Action<BetCancelledEventArgs>? BetCancelled;

    /// <summary>Notifies listeners that the bet has been placed.</summary>
    internal void NotifyBetPlaced(BetPlacedEventArgs payload) => BetPlaced?.Invoke(payload);

    /// <summary>Notifies listeners that the bet has been cancelled.</summary>
    internal void NotifyBetCancelled(BetCancelledEventArgs payload) => BetCancelled?.Invoke(payload);
}
