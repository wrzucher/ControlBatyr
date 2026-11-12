using Game.Model;

namespace Game.Core;

internal static class BetSpecification
{
    internal static IReadOnlyList<ServiceErrorCode> CanCancel(Bet bet, DateTimeOffset now)
    {
        var problems = new List<ServiceErrorCode>();
        if (!bet.IsOpen)
        {
            problems.Add(ServiceErrorCode.BetAlreadyCancelled);
        }
        if (now >= bet.GameStartsAt)
        {
            problems.Add(ServiceErrorCode.GameStarted);
        }
        return problems;
    }
}
