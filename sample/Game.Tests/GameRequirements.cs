using Game.Core;
using Game.Model;
using Game.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using GameModel = Game.Model.Game;

namespace Game.Tests;

public sealed class GameRequirements : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly GamePersistence.GameContext context;
    private readonly FakeTimeProvider clock = new(new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly GameEventPublisher publisher = new();
    private readonly GameManager manager;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public GameRequirements()
    {
        var options = new DbContextOptionsBuilder<GamePersistence.GameContext>().UseSqlite(connection).Options;
        context = new GamePersistence.GameContext(options);
        manager = new GameManager(new GamePersistence(context), publisher, clock);
    }

    public async ValueTask InitializeAsync()
    {
        await connection.OpenAsync(Ct);
        await context.Database.EnsureCreatedAsync(Ct);
    }

    [Fact]
    public async Task Placed_bet_preserves_the_player_game_and_amount()
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
    public async Task Cancelled_bet_is_recorded_and_its_identifier_is_published()
    {
        var bet = await OpenBetAsync();
        var notifications = new List<BetCancelledEventArgs>();
        void ObserveCancellation(BetCancelledEventArgs payload) => notifications.Add(payload);
        publisher.BetCancelled += ObserveCancellation;
        try
        {
            await manager.CancelBetAsync(bet.Id, Ct);

            Assert.Equal([new BetCancelledEventArgs(bet.Id, bet.PlayerId, bet.Amount)], notifications);
            var cancelledBet = await RecordedBetAsync(bet.Id);
            Assert.Equal(BetState.Cancelled, cancelledBet.State);
        }
        finally
        {
            publisher.BetCancelled -= ObserveCancellation;
        }
    }

    [Fact]
    public async Task Cancellation_preview_explains_all_reasons_the_bet_cannot_be_cancelled()
    {
        var bet = await CancelledBetAsync();
        clock.SetUtcNow(bet.GameStartsAt.AddMinutes(1));

        var problems = await manager.CancelBetProblemAsync(bet.Id, Ct);

        Assert.Equal([ServiceErrorCode.BetAlreadyCancelled, ServiceErrorCode.GameStarted], problems);
    }

    [Fact]
    public async Task Placed_bet_is_published_with_its_recorded_data()
    {
        var notifications = new List<BetPlacedEventArgs>();
        void ObservePlacement(BetPlacedEventArgs payload) => notifications.Add(payload);
        publisher.BetPlaced += ObservePlacement;
        try
        {
            var bet = await OpenBetAsync();

            Assert.Equal([new BetPlacedEventArgs(bet)], notifications);
            Assert.Equal(bet, await RecordedBetAsync(bet.Id));
        }
        finally
        {
            publisher.BetPlaced -= ObservePlacement;
        }
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

    public async ValueTask DisposeAsync()
    {
        await context.DisposeAsync();
        await connection.DisposeAsync();
    }
}
