namespace ControlBatyr;

/// <summary>Connects a publisher event to a static action taking a manager and the event payload.</summary>
/// <remarks>Actions are synchronous void methods. Name the action after the target manager operation
/// to link to that operation in generated documentation.</remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class OnAttribute<TPublisher>(string eventName) : Attribute
{
    public string EventName { get; } = eventName;
}
