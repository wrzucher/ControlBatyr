namespace ControlBatyr;

/// <summary>Declares a type as the event publisher of the domain of TManager.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class EventPublisherAttribute<TManager> : Attribute;
