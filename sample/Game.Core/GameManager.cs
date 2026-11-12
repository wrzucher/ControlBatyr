using ControlBatyr;
using Game.Model;
using Game.Persistence;
using GameModel = Game.Model.Game;

namespace Game.Core;

[Manager]
public sealed class GameManager(GamePersistence games, GameEventPublisher publisher, TimeProvider clock)
{
    /// <summary>Sets when the game starts and bets can no longer be cancelled.</summary>
    public Task<GameModel> CreateGameAsync(string name, DateTimeOffset startsAt, CancellationToken ct = default) =>
        games.CreateGameAsync(name, startsAt, ct);

    /// <summary>Places a player's bet on an existing game.</summary>
    public async Task<Bet> PlaceBetAsync(Guid gameId, Guid playerId, decimal amount, CancellationToken ct = default)
    {
        var game = await games.GameAsync(gameId, ct)
            ?? throw new ServiceRefusalException([ServiceErrorCode.GameNotFound]);

        var bet = await games.PlaceBetAsync(game.Id, playerId, amount, ct);

        publisher.BetPlaced(bet);
        return bet;
    }

    /// <summary>Explains why the player cannot cancel the bet.</summary>
    public async Task<IReadOnlyList<ServiceErrorCode>> CancelBetProblemAsync(Guid betId, CancellationToken ct = default)
    {
        var bet = await games.BetAsync(betId, ct);
        return bet is null ? [ServiceErrorCode.BetNotFound] : BetSpecification.CanCancel(bet, clock.GetUtcNow());
    }

    /// <summary>Lets the player cancel an open bet before the game starts.</summary>
    public async Task CancelBetAsync(Guid betId, CancellationToken ct = default)
    {
        Require(await CancelBetProblemAsync(betId, ct));

        await games.CancelBetAsync(betId, ct);

        publisher.BetCancelled(betId);
    }

    /// <summary>Lets the player see the bet's current state.</summary>
    public Task<Bet?> BetAsync(Guid betId, CancellationToken ct = default) => games.BetAsync(betId, ct);

    private static void Require(IReadOnlyList<ServiceErrorCode> problems)
    {
        if (problems.Count > 0)
        {
            throw new ServiceRefusalException(problems);
        }
    }
}
