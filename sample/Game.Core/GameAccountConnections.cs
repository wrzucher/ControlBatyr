using ControlBatyr;
using Game.Model;

namespace Game.Core;

internal static class GameAccountConnections
{
    /// <summary>Return the bet amount to the player's account after cancellation.</summary>
    [On<GameEventPublisher>(nameof(GameEventPublisher.BetCancelled))]
    internal static Task RefundBetAsync(AccountManager accounts, BetCancelledEventArgs e) =>
        accounts.RefundBetAsync(e.PlayerId, e.BetId, e.Amount);
}
