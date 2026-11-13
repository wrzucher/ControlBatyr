namespace ControlBatyr;

/// <summary>Declares an event raised by an operation.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class RaisesAttribute<TPublisher> : Attribute
{
    public RaisesAttribute() { }

    public RaisesAttribute(string eventName) => EventName = eventName;

    public string? EventName { get; }
}
