using ControlBatyr;
using Game.Model;
using Game.Persistence;

namespace Game.Core;

/// <summary>Holds players' funds and returns the amounts of cancelled bets.</summary>
[Manager]
public sealed class AccountManager(AccountPersistence accounts)
{
    /// <summary>Opens an account with the player's initial funds.</summary>
    public Account OpenAccount(Guid playerId, decimal initialBalance) => accounts.OpenAccount(playerId, initialBalance);

    /// <summary>Shows the player's available funds.</summary>
    public Account Account(Guid playerId) => accounts.Account(playerId);

    /// <summary>Returns the cancelled bet's amount to the player who placed it.</summary>
    public void RefundBet(Guid playerId, Guid betId, decimal amount) => accounts.RefundBet(playerId, betId, amount);
}
