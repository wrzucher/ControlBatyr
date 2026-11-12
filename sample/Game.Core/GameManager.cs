using ControlBatyr;
using Game.Model;
using Game.Persistence;
using GameModel = Game.Model.Game;

namespace Game.Core;

/// <summary>Lets players place bets and cancel them until their game starts.</summary>
[Manager]
public sealed class GameManager(GamePersistence games, GameEventPublisher publisher, TimeProvider clock)
{
    /// <summary>Sets when the game starts and bets can no longer be cancelled.</summary>
    /// <param name="name">The title players use to recognise the game.</param>
    /// <param name="startsAt">When cancellation closes for every bet on this game.</param>
    /// <param name="ct"/>
    /// <returns>The recorded game, including the identifier used to place bets.</returns>
    public Task<GameModel> CreateGameAsync(string name, DateTimeOffset startsAt, CancellationToken ct = default) =>
        games.CreateGameAsync(name, startsAt, ct);

    /// <summary>Places a player's bet on an existing game.</summary>
    /// <param name="gameId">The game the player chose.</param>
    /// <param name="playerId">The player who owns the bet.</param>
    /// <param name="amount">The amount the player bets.</param>
    /// <param name="ct"/>
    /// <returns>The recorded bet in its initial open state.</returns>
    public async Task<Bet> PlaceBetAsync(Guid gameId, Guid playerId, decimal amount, CancellationToken ct = default)
    {
        var game = await games.GameAsync(gameId, ct)
            ?? throw new ServiceRefusalException([ServiceErrorCode.GameNotFound]);

        var bet = await games.PlaceBetAsync(game.Id, playerId, amount, ct);

        publisher.NotifyBetPlaced(new BetPlacedEventArgs(bet));
        return bet;
    }

    /// <summary>Explains why the player cannot cancel the bet.</summary>
    /// <param name="betId">The bet whose cancellation the player is considering.</param>
    /// <param name="ct"/>
    /// <returns>All current reasons preventing cancellation; empty when cancellation is allowed.</returns>
    /// <remarks>This preview does not change the bet. <see cref="CancelBetAsync">CancelBet</see> checks again before cancelling it.</remarks>
    public async Task<IReadOnlyList<ServiceErrorCode>> CancelBetProblemAsync(Guid betId, CancellationToken ct = default)
    {
        var bet = await games.BetAsync(betId, ct);
        return bet is null ? [ServiceErrorCode.BetNotFound] : BetSpecification.CanCancel(bet, clock.GetUtcNow());
    }

    /// <summary>Lets the player cancel an open bet before the game starts.</summary>
    /// <param name="betId">The bet the player wants to cancel.</param>
    /// <param name="ct"/>
    /// <remarks>Listeners are notified after the cancellation has been recorded.</remarks>
    public async Task CancelBetAsync(Guid betId, CancellationToken ct = default)
    {
        Require(await CancelBetProblemAsync(betId, ct));

        await games.CancelBetAsync(betId, ct);

        publisher.NotifyBetCancelled(new BetCancelledEventArgs(betId));
    }

    /// <summary>Lets the player see the bet's current state.</summary>
    /// <param name="betId">The bet to look up.</param>
    /// <param name="ct"/>
    /// <returns>The current bet, or <see langword="null"/> when <paramref name="betId"/> is unknown.</returns>
    public Task<Bet?> BetAsync(Guid betId, CancellationToken ct = default) => games.BetAsync(betId, ct);

    private static void Require(IReadOnlyList<ServiceErrorCode> problems)
    {
        if (problems.Count > 0)
        {
            throw new ServiceRefusalException(problems);
        }
    }
}
