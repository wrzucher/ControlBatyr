using ControlBatyr;
using Game.Model;
using Microsoft.EntityFrameworkCore;

namespace Game.Persistence;

[Persistence]
public sealed class AccountPersistence(DbContext context)
{
    public Account OpenAccount(Guid playerId, decimal initialBalance)
    {
        context.Set<AccountEntity>().Add(new AccountEntity { PlayerId = playerId, Balance = initialBalance });
        context.SaveChanges();
        return Account(playerId);
    }

    public Account Account(Guid playerId) => context.Set<AccountEntity>().AsNoTracking()
        .Where(account => account.PlayerId == playerId)
        .Select(account => new Account(account.PlayerId, account.Balance)).Single();

    public void RefundBet(Guid playerId, Guid betId, decimal amount)
    {
        var account = context.Set<AccountEntity>().Single(account => account.PlayerId == playerId);
        account.Balance += amount;
        context.Set<RefundEntity>().Add(new RefundEntity { BetId = betId, PlayerId = playerId, Amount = amount });
        context.SaveChanges();
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
