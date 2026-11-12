using ControlBatyr;
using Game.Model;
using Microsoft.EntityFrameworkCore;
using GameModel = Game.Model.Game;

namespace Game.Persistence;

[Persistence]
public sealed class GamePersistence(DbContext context)
{
    public async Task<GameModel> CreateGameAsync(string name, DateTimeOffset startsAt, CancellationToken ct)
    {
        var game = new GameEntity { Id = Guid.NewGuid(), Name = name, StartsAt = startsAt };
        context.Set<GameEntity>().Add(game);
        await context.SaveChangesAsync(ct);
        return new GameModel(game.Id, game.Name, game.StartsAt);
    }

    public async Task<GameModel?> GameAsync(Guid gameId, CancellationToken ct)
    {
        var game = await context.Set<GameEntity>().AsNoTracking().SingleOrDefaultAsync(game => game.Id == gameId, ct);
        return game is null ? null : new GameModel(game.Id, game.Name, game.StartsAt);
    }

    public async Task<Bet> PlaceBetAsync(Guid gameId, Guid playerId, decimal amount, CancellationToken ct)
    {
        var bet = new BetEntity
        {
            Id = Guid.NewGuid(), GameId = gameId, PlayerId = playerId, Amount = amount, State = BetState.Open
        };
        context.Set<BetEntity>().Add(bet);
        await context.SaveChangesAsync(ct);
        return (await BetAsync(bet.Id, ct))!;
    }

    public Task<Bet?> BetAsync(Guid betId, CancellationToken ct) =>
        context.Set<BetEntity>().AsNoTracking()
            .Where(bet => bet.Id == betId)
            .Select(bet => new Bet(bet.Id, bet.GameId, bet.PlayerId, bet.Amount, bet.State, bet.Game.StartsAt))
            .SingleOrDefaultAsync(ct);

    public async Task CancelBetAsync(Guid betId, CancellationToken ct)
    {
        var changed = await context.Set<BetEntity>().Where(bet => bet.Id == betId && bet.State == BetState.Open)
            .ExecuteUpdateAsync(update => update.SetProperty(bet => bet.State, BetState.Cancelled), ct);
        if (changed != 1)
        {
            throw new InvalidOperationException("The bet is no longer open.");
        }
    }

    internal sealed class GameContext(DbContextOptions<GameContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BetEntity>().HasOne(bet => bet.Game).WithMany().HasForeignKey(bet => bet.GameId);
        }
    }

    private sealed class GameEntity
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public DateTimeOffset StartsAt { get; set; }
    }

    private sealed class BetEntity
    {
        public Guid Id { get; set; }
        public Guid GameId { get; set; }
        public Guid PlayerId { get; set; }
        public decimal Amount { get; set; }
        public BetState State { get; set; }
        public GameEntity Game { get; set; } = null!;
    }
}
