using ControlBatyr;
using Game.Model;

namespace Game.Core;

internal static class GameAccountConnections
{
    /// <summary>Return the cancelled bet's stake to its player's account.</summary>
    [On<GameEventPublisher>(nameof(GameEventPublisher.BetCancelled))]
    internal static void RefundBet(AccountManager accounts, BetCancelledEventArgs e) =>
        accounts.RefundBet(e.PlayerId, e.BetId, e.Amount);
}
