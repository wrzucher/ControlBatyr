using ControlBatyr;
using Game.Model;
using Game.Persistence;

namespace Game.Core;

/// <summary>Holds players' funds and returns the amounts of cancelled bets.</summary>
[Manager]
public sealed class AccountManager(AccountPersistence accounts)
{
    /// <summary>Opens an account with the player's initial funds.</summary>
    public Task<Account> OpenAccountAsync(Guid playerId, decimal initialBalance, CancellationToken ct = default) =>
        accounts.OpenAccountAsync(playerId, initialBalance, ct);

    /// <summary>Shows the player's available funds.</summary>
    public Task<Account> AccountAsync(Guid playerId, CancellationToken ct = default) => accounts.AccountAsync(playerId, ct);

    /// <summary>Returns the cancelled bet's amount to the player who placed it.</summary>
    public Task RefundBetAsync(Guid playerId, Guid betId, decimal amount, CancellationToken ct = default) =>
        accounts.RefundBetAsync(playerId, betId, amount, ct);
}
