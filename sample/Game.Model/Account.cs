using ControlBatyr;

namespace Game.Model;

/// <summary>The funds available to a player.</summary>
[ObjectModel]
public sealed record Account(Guid PlayerId, decimal Balance);
