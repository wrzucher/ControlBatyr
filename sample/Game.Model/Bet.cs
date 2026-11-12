using ControlBatyr;

namespace Game.Model;

[ObjectModel]
public sealed record Bet(
    Guid Id, Guid GameId, Guid PlayerId, decimal Amount, BetState State, DateTimeOffset GameStartsAt)
{
    public bool IsOpen => State == BetState.Open;
}
