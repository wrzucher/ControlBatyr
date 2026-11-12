using ControlBatyr;

namespace Game.Model;

/// <summary>A scheduled game that players can bet on.</summary>
/// <param name="Id">Identifies the game when placing a bet.</param>
/// <param name="Name">The title shown to players.</param>
/// <param name="StartsAt">When play begins and cancellation closes.</param>
[ObjectModel]
public sealed record Game(Guid Id, string Name, DateTimeOffset StartsAt);
