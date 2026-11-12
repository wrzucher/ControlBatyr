using ControlBatyr;
using Game.Model;
using Microsoft.EntityFrameworkCore;

namespace Game.Persistence;

[Persistence]
public sealed class AccountPersistence(DbContext context)
{
    public async Task<Account> OpenAccountAsync(Guid playerId, decimal initialBalance, CancellationToken ct)
    {
        context.Set<AccountEntity>().Add(new AccountEntity { PlayerId = playerId, Balance = initialBalance });
        await context.SaveChangesAsync(ct);
        return await AccountAsync(playerId, ct);
    }

    public Task<Account> AccountAsync(Guid playerId, CancellationToken ct) => context.Set<AccountEntity>().AsNoTracking()
        .Where(account => account.PlayerId == playerId)
        .Select(account => new Account(account.PlayerId, account.Balance)).SingleAsync(ct);

    public async Task RefundBetAsync(Guid playerId, Guid betId, decimal amount, CancellationToken ct)
    {
        var account = await context.Set<AccountEntity>().SingleAsync(account => account.PlayerId == playerId, ct);
        account.Balance += amount;
        context.Set<RefundEntity>().Add(new RefundEntity { BetId = betId, PlayerId = playerId, Amount = amount });
        await context.SaveChangesAsync(ct);
    }

    internal sealed class AccountContext(DbContextOptions<AccountContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AccountEntity>().HasKey(account => account.PlayerId);
            modelBuilder.Entity<RefundEntity>().HasKey(refund => refund.BetId);
        }
    }

    private sealed class AccountEntity
    {
        public Guid PlayerId { get; set; }
        public decimal Balance { get; set; }
    }

    private sealed class RefundEntity
    {
        public Guid BetId { get; set; }
        public Guid PlayerId { get; set; }
        public decimal Amount { get; set; }
    }
}
