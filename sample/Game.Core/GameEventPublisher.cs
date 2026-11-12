using ControlBatyr;
using Game.Model;

namespace Game.Core;

[EventPublisher]
public sealed class GameEventPublisher
{
    /// <summary>The player's bet has been placed.</summary>
    public event Action<Bet>? BetPlaced;

    /// <summary>The player's bet has been cancelled.</summary>
    public event Action<Guid>? BetCancelled;

    /// <summary>Notifies listeners that the bet has been placed.</summary>
    internal void NotifyBetPlaced(Bet bet) => BetPlaced?.Invoke(bet);

    /// <summary>Notifies listeners that the bet has been cancelled.</summary>
    internal void NotifyBetCancelled(Guid betId) => BetCancelled?.Invoke(betId);
}
