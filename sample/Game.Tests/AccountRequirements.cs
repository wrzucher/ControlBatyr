using ControlBatyr;
using Game.Core;
using Game.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Game.Tests;

public sealed class AccountRequirements
{
    [Fact]
    public async Task Cancelling_a_bet_refunds_its_stake_to_its_players_account_through_DI()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var gameConnection = new SqliteConnection("Data Source=:memory:");
        await using var accountConnection = new SqliteConnection("Data Source=:memory:");
        await gameConnection.OpenAsync(ct);
        await accountConnection.OpenAsync(ct);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => new GamePersistence.GameContext(
            new DbContextOptionsBuilder<GamePersistence.GameContext>().UseSqlite(gameConnection).Options));
        services.AddScoped(_ => new AccountPersistence.AccountContext(
            new DbContextOptionsBuilder<AccountPersistence.AccountContext>().UseSqlite(accountConnection).Options));
        services.AddScoped(provider => new GamePersistence(provider.GetRequiredService<GamePersistence.GameContext>()));
        services.AddScoped(provider => new AccountPersistence(provider.GetRequiredService<AccountPersistence.AccountContext>()));
        services.AddScoped<GameManager>();
        services.AddScoped<AccountManager>();
        var clock = new FakeTimeProvider(new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero));
        services.AddSingleton<TimeProvider>(clock);
        services.AddConnections(typeof(GameManager).Assembly);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using (var setup = provider.CreateAsyncScope())
        {
            await setup.ServiceProvider.GetRequiredService<GamePersistence.GameContext>().Database.EnsureCreatedAsync(ct);
            await setup.ServiceProvider.GetRequiredService<AccountPersistence.AccountContext>().Database.EnsureCreatedAsync(ct);
        }
        var playerId = Guid.NewGuid();
        var otherPlayer = Guid.NewGuid();
        await using (var scope = provider.CreateAsyncScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<AccountManager>();
            accounts.OpenAccount(playerId, 10m);
            accounts.OpenAccount(otherPlayer, 80m);
            var games = scope.ServiceProvider.GetRequiredService<GameManager>();
            var game = await games.CreateGameAsync("Moon race", clock.GetUtcNow().AddHours(2), ct);
            var bet = await games.PlaceBetAsync(game.Id, playerId, 25m, ct);
            await games.CancelBetAsync(bet.Id, ct);
        }
        await using var reading = provider.CreateAsyncScope();
        var balances = reading.ServiceProvider.GetRequiredService<AccountManager>();
        Assert.Equal(35m, balances.Account(playerId).Balance);
        Assert.Equal(80m, balances.Account(otherPlayer).Balance);
    }
}
