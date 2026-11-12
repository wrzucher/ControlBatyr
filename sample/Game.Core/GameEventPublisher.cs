using ControlBatyr;
using Game.Model;

namespace Game.Core;

[EventPublisher]
public sealed class GameEventPublisher
{
    public static GameEventPublisher Instance { get; } = new();

    private GameEventPublisher() { }

    /// <summary>Allows listeners to react to a player's commitment to a game.</summary>
    public event Action<Bet>? OnBetPlaced;

    /// <summary>Allows listeners to release obligations associated with the bet.</summary>
    public event Action<Guid>? OnBetCancelled;

    /// <summary>Makes the player's commitment visible after it has been recorded.</summary>
    internal void BetPlaced(Bet bet) => OnBetPlaced?.Invoke(bet);

    /// <summary>Makes the release of the player's commitment visible after cancellation.</summary>
    internal void BetCancelled(Guid betId) => OnBetCancelled?.Invoke(betId);
}
