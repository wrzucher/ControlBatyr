using ControlBatyr;
using Game.Model;
using Game.Persistence;
using GameModel = Game.Model.Game;

namespace Game.Core;

[Manager]
public sealed class GameManager(GamePersistence games, GameEventPublisher publisher, TimeProvider clock)
{
    /// <summary>Establishes the game and the deadline for withdrawing bets.</summary>
    public Task<GameModel> CreateGameAsync(string name, DateTimeOffset startsAt, CancellationToken ct = default) =>
        games.CreateGameAsync(name, startsAt, ct);

    /// <summary>Records the player's commitment so it can be tracked or withdrawn.</summary>
    public async Task<Bet> PlaceBetAsync(Guid gameId, Guid playerId, decimal amount, CancellationToken ct = default)
    {
        var game = await games.GameAsync(gameId, ct);
        Require(game is null ? [ServiceErrorCode.GameNotFound] : []);

        var bet = await games.PlaceBetAsync(gameId, playerId, amount, ct);

        publisher.BetPlaced(bet);
        return bet;
    }

    /// <summary>Explains whether the player can still withdraw the commitment.</summary>
    public async Task<IReadOnlyList<ServiceErrorCode>> CancelBetProblemAsync(Guid betId, CancellationToken ct = default)
    {
        var bet = await games.BetAsync(betId, ct);
        return bet is null ? [ServiceErrorCode.BetNotFound] : BetSpecification.CanCancel(bet, clock.GetUtcNow());
    }

    /// <summary>Releases the player's commitment while withdrawal is still allowed.</summary>
    public async Task CancelBetAsync(Guid betId, CancellationToken ct = default)
    {
        Require(await CancelBetProblemAsync(betId, ct));

        await games.CancelBetAsync(betId, ct);

        publisher.BetCancelled(betId);
    }

    /// <summary>Lets the player inspect the current commitment to a game.</summary>
    public Task<Bet?> BetAsync(Guid betId, CancellationToken ct = default) => games.BetAsync(betId, ct);

    private static void Require(IReadOnlyList<ServiceErrorCode> problems)
    {
        if (problems.Count > 0)
        {
            throw new ServiceRefusalException(problems);
        }
    }
}
