using ControlBatyr.Requirements;
using ControlBatyr;
using Game.Core;
using Game.Model;
using Game.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Game.Tests;

public sealed class AccountRequirements : IAsyncLifetime
{
    private readonly SqliteConnection gameConnection = new("Data Source=:memory:");
    private readonly SqliteConnection accountConnection = new("Data Source=:memory:");
    private readonly FakeTimeProvider clock = new(new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly RefundCompletion refund = new();
    private readonly ServiceProvider provider;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AccountRequirements()
    {
        var services = new ServiceCollection().AddGameCore(
            options => options.UseSqlite(gameConnection),
            options => options.UseSqlite(accountConnection).AddInterceptors(refund), clock);
        provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        provider.ConnectDomains(typeof(GameManager).Assembly);
    }

    [Fact]
    [Requirement<GameManager>(nameof(GameManager.CancelBetAsync))]
    [Requirement<AccountManager>(nameof(AccountManager.RefundBetAsync))]
    public async Task Cancelling_a_bet_refunds_its_bet_amount_to_its_players_account()
    {
        var playerId = await PlayerWithAccountAsync(10m);
        var otherPlayerId = await PlayerWithAccountAsync(80m);
        var bet = await PlaceBetAsync(playerId, 25m);

        await CancelBetAsync(bet.Id);

        var account = await AccountAfterRefundAsync(playerId);
        Assert.Equal(35m, account.Balance);
        Assert.Equal(80m, (await AccountAsync(otherPlayerId)).Balance);
    }

    public async ValueTask InitializeAsync()
    {
        await gameConnection.OpenAsync(Ct);
        await accountConnection.OpenAsync(Ct);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<GamePersistence.GameContext>().Database.EnsureCreatedAsync(Ct);
        await scope.ServiceProvider.GetRequiredService<AccountPersistence.AccountContext>().Database.EnsureCreatedAsync(Ct);
    }

    private async Task<Guid> PlayerWithAccountAsync(decimal balance)
    {
        var playerId = Guid.NewGuid();
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AccountManager>().OpenAccountAsync(playerId, balance, Ct);
        return playerId;
    }

    private async Task<Bet> PlaceBetAsync(Guid playerId, decimal amount)
    {
        await using var scope = provider.CreateAsyncScope();
        var games = scope.ServiceProvider.GetRequiredService<GameManager>();
        var game = await games.CreateGameAsync("Moon race", clock.GetUtcNow().AddHours(2), Ct);
        return await games.PlaceBetAsync(game.Id, playerId, amount, Ct);
    }

    private async Task CancelBetAsync(Guid betId)
    {
        refund.Enabled = true;
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<GameManager>().CancelBetAsync(betId, Ct);
    }

    private async Task<Account> AccountAfterRefundAsync(Guid playerId)
    {
        await refund.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        return await AccountAsync(playerId);
    }

    private async Task<Account> AccountAsync(Guid playerId)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AccountManager>().AccountAsync(playerId, Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await provider.DisposeAsync();
        await gameConnection.DisposeAsync();
        await accountConnection.DisposeAsync();
    }

    private sealed class RefundCompletion : SaveChangesInterceptor
    {
        internal bool Enabled { get; set; }
        internal TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData,
            int result, CancellationToken cancellationToken = default)
        {
            if (Enabled)
            {
                Completed.TrySetResult();
            }
            return ValueTask.FromResult(result);
        }
    }
}
