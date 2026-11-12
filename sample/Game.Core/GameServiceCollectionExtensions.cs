using Game.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Game.Core;

public static class GameServiceCollectionExtensions
{
    public static IServiceCollection AddGameCore(this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureGames, Action<DbContextOptionsBuilder> configureAccounts,
        TimeProvider? clock = null)
    {
        services.AddLogging();
        services.AddDbContext<GamePersistence.GameContext>(configureGames);
        services.AddDbContext<AccountPersistence.AccountContext>(configureAccounts);
        services.AddScoped(provider => new GamePersistence(provider.GetRequiredService<GamePersistence.GameContext>()));
        services.AddScoped(provider => new AccountPersistence(provider.GetRequiredService<AccountPersistence.AccountContext>()));
        services.AddScoped<GameManager>();
        services.AddScoped<AccountManager>();
        services.AddSingleton<GameEventPublisher>();
        services.AddSingleton(clock ?? TimeProvider.System);
        return services;
    }
}
