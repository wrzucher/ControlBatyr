namespace ControlBatyr;

/// <summary>Declares an event raised by an operation.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class RaisesAttribute<TPublisher>(string eventName) : Attribute
{
    public string EventName { get; } = eventName;
}
