using Game.Core;
using Game.Model;
using Game.Persistence;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using GameModel = Game.Model.Game;

namespace Game.Tests;

public sealed class GameRequirements : IDisposable
{
    private readonly GamePersistence persistence = new();
    private readonly FakeTimeProvider clock = new(new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly GameEventPublisher publisher = GameEventPublisher.Instance;
    private readonly GameManager manager;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public GameRequirements() => manager = new GameManager(persistence, publisher, clock);

    [Fact]
    public async Task Placed_bet_preserves_the_players_commitment_to_the_game()
    {
        var game = await UpcomingGameAsync();
        var playerId = Guid.NewGuid();

        var bet = await manager.PlaceBetAsync(game.Id, playerId, 25m, Ct);

        Assert.NotEqual(Guid.Empty, bet.Id);
        Assert.Equal(game.Id, bet.GameId);
        Assert.Equal(playerId, bet.PlayerId);
        Assert.Equal(25m, bet.Amount);
        Assert.Equal(game.StartsAt, bet.GameStartsAt);
        Assert.True(bet.IsOpen);
        Assert.Equal(bet, await RecordedBetAsync(bet.Id));
    }

    [Fact]
    public async Task Open_bet_can_be_cancelled_before_the_game_starts()
    {
        var bet = await OpenBetAsync();

        await manager.CancelBetAsync(bet.Id, Ct);

        var cancelledBet = await RecordedBetAsync(bet.Id);
        Assert.Equal(BetState.Cancelled, cancelledBet.State);
        Assert.False(cancelledBet.IsOpen);
    }

    [Fact]
    public async Task Bet_cannot_be_cancelled_once_the_game_starts()
    {
        var bet = await OpenBetAsync();
        clock.SetUtcNow(bet.GameStartsAt);

        var refusal = await Assert.ThrowsAsync<ServiceRefusalException>(() => manager.CancelBetAsync(bet.Id, Ct));

        Assert.Equal([ServiceErrorCode.GameStarted], refusal.Codes);
        Assert.Equal(bet, await RecordedBetAsync(bet.Id));
    }

    [Fact]
    public async Task Cancelled_bet_cannot_be_cancelled_again()
    {
        var bet = await CancelledBetAsync();

        var refusal = await Assert.ThrowsAsync<ServiceRefusalException>(() => manager.CancelBetAsync(bet.Id, Ct));

        Assert.Equal([ServiceErrorCode.BetAlreadyCancelled], refusal.Codes);
        Assert.Equal(BetState.Cancelled, (await RecordedBetAsync(bet.Id)).State);
    }

    [Fact]
    public async Task Cancellation_notifies_listeners_after_the_bet_is_recorded_as_cancelled()
    {
        var bet = await OpenBetAsync();
        var notifications = new List<Guid>();
        Bet? betAtNotification = null;
        void ObserveCancellation(Guid betId)
        {
            notifications.Add(betId);
            betAtNotification = RecordedBetAsync(betId).GetAwaiter().GetResult();
        }
        publisher.OnBetCancelled += ObserveCancellation;
        try
        {
            await manager.CancelBetAsync(bet.Id, Ct);

            Assert.Equal([bet.Id], notifications);
            Assert.NotNull(betAtNotification);
            Assert.Equal(BetState.Cancelled, betAtNotification.State);
        }
        finally
        {
            publisher.OnBetCancelled -= ObserveCancellation;
        }
    }

    [Fact]
    public async Task Cancellation_preview_explains_all_reasons_the_bet_cannot_be_withdrawn()
    {
        var bet = await CancelledBetAsync();
        clock.SetUtcNow(bet.GameStartsAt.AddMinutes(1));

        var problems = await manager.CancelBetProblemAsync(bet.Id, Ct);

        Assert.Equal([ServiceErrorCode.BetAlreadyCancelled, ServiceErrorCode.GameStarted], problems);
    }

    private Task<GameModel> UpcomingGameAsync() =>
        manager.CreateGameAsync("Moon race", clock.GetUtcNow().AddHours(2), Ct);

    private async Task<Bet> OpenBetAsync()
    {
        var game = await UpcomingGameAsync();
        return await manager.PlaceBetAsync(game.Id, Guid.NewGuid(), 25m, Ct);
    }

    private async Task<Bet> CancelledBetAsync()
    {
        var bet = await OpenBetAsync();
        await manager.CancelBetAsync(bet.Id, Ct);
        return bet;
    }

    private async Task<Bet> RecordedBetAsync(Guid betId) =>
        Assert.IsType<Bet>(await manager.BetAsync(betId, Ct));

    public void Dispose() => persistence.Dispose();
}
