using System.Reflection;

namespace ControlBatyr.Inspection;

internal sealed record Connection(Type Publisher, EventInfo Event, MethodInfo Method, Type Manager)
{
    internal static Connection[] Find(Assembly[] assemblies) => assemblies.Distinct()
        .OrderBy(assembly => assembly.FullName, StringComparer.Ordinal)
        .SelectMany(assembly => assembly.GetTypes().OrderBy(type => type.MetadataToken))
        .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
            | BindingFlags.Instance | BindingFlags.DeclaredOnly).OrderBy(method => method.MetadataToken))
        .SelectMany(method => method.CustomAttributes
            .Where(attribute => attribute.AttributeType.IsGenericType
                && attribute.AttributeType.GetGenericTypeDefinition().IsType("ControlBatyr.OnAttribute`1"))
            .Select(attribute => Create(method, attribute))).ToArray();

    private static Connection Create(MethodInfo method, CustomAttributeData attribute)
    {
        var publisher = attribute.AttributeType.GetGenericArguments()[0];
        var name = (string)attribute.ConstructorArguments[0].Value!;
        var item = publisher.GetEvent(name);
        var parameters = method.GetParameters();
        if (!publisher.HasAttribute<EventPublisherAttribute>() || item?.AddMethod?.IsStatic != false
            || !method.IsStatic || method.ContainsGenericParameters || !method.ReturnType.IsType("System.Threading.Tasks.Task")
            || parameters.Length != 2 || !parameters[0].ParameterType.HasAttribute<ManagerAttribute>()
            || parameters.Any(parameter => parameter.ParameterType.IsByRef)
            || (!item.EventHandlerType!.IsGenericType || !item.EventHandlerType.GetGenericTypeDefinition().IsType("System.Action`1")
                || item.EventHandlerType.GetGenericArguments()[0] != parameters[1].ParameterType))
        {
            throw new InvalidOperationException($"Connection {method.DeclaringType?.FullName}.{method.Name} must be a static Task method taking a [Manager] and the payload of {publisher.Name}.{name}, a public instance Action<T> event.");
        }
        return new Connection(publisher, item, method, parameters[0].ParameterType);
    }
}
