using ControlBatyr;

namespace Game.Model;

[ObjectModel]
public sealed record Game(Guid Id, string Name, DateTimeOffset StartsAt);
