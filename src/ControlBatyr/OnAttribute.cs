namespace ControlBatyr;

/// <summary>Connects a publisher event to a static action taking a manager and the event payload.</summary>
/// <remarks>Actions return Task and take a manager and the event payload.</remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class OnAttribute<TPublisher>(string eventName) : Attribute
{
    public string EventName { get; } = eventName;
}
