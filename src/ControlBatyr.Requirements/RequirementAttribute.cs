namespace ControlBatyr.Requirements;

/// <summary>Links a test to a public operation of a manager; the test summary or name supplies the requirement.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class RequirementAttribute<TManager>(string operation) : Attribute
{
    public string Operation { get; } = operation;
}
