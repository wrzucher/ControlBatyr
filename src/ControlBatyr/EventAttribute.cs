namespace ControlBatyr;

/// <summary>Declares a type as an event of the domain of TManager.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class EventAttribute<TManager> : Attribute;
