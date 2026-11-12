using ControlBatyr;
using Game.Model;

namespace Game.Core;

[EventPublisher]
public sealed class GameEventPublisher
{
    /// <summary>The player's bet has been placed.</summary>
    public event Action<Bet>? OnBetPlaced;

    /// <summary>The player's bet has been cancelled.</summary>
    public event Action<Guid>? OnBetCancelled;

    /// <summary>Notifies listeners that the bet has been placed.</summary>
    internal void BetPlaced(Bet bet) => OnBetPlaced?.Invoke(bet);

    /// <summary>Notifies listeners that the bet has been cancelled.</summary>
    internal void BetCancelled(Guid betId) => OnBetCancelled?.Invoke(betId);
}
