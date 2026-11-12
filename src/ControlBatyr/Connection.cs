using System.Reflection;
using System.Runtime.CompilerServices;

namespace ControlBatyr;

internal sealed record Connection(Type Publisher, EventInfo Event, MethodInfo Method, Type Manager)
{
    internal static Connection[] Find(Assembly[] assemblies) => assemblies.Distinct()
        .OrderBy(assembly => assembly.FullName, StringComparer.Ordinal)
        .SelectMany(assembly => assembly.GetTypes().OrderBy(type => type.MetadataToken))
        .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
            | BindingFlags.Instance | BindingFlags.DeclaredOnly).OrderBy(method => method.MetadataToken))
        .SelectMany(method => method.CustomAttributes
            .Where(attribute => attribute.AttributeType.IsGenericType
                && attribute.AttributeType.GetGenericTypeDefinition() == typeof(OnAttribute<>))
            .Select(attribute => Create(method, attribute))).ToArray();

    private static Connection Create(MethodInfo method, CustomAttributeData attribute)
    {
        var publisher = attribute.AttributeType.GetGenericArguments()[0];
        var name = (string)attribute.ConstructorArguments[0].Value!;
        var item = publisher.GetEvent(name);
        var parameters = method.GetParameters();
        var invoke = item?.EventHandlerType?.GetMethod("Invoke");
        var eventParameters = invoke?.GetParameters();
        if (!publisher.IsDefined(typeof(EventPublisherAttribute), false) || item is null
            || item.AddMethod?.IsStatic != false || invoke?.ReturnType != typeof(void)
            || eventParameters is null || eventParameters.Length is < 1 or > 2
            || (eventParameters.Length == 2 && eventParameters[0].ParameterType != typeof(object))
            || !method.IsStatic || method.ContainsGenericParameters || method.ReturnType != typeof(void)
            || method.IsDefined(typeof(AsyncStateMachineAttribute), false)
            || parameters.Length != 2 || !parameters[0].ParameterType.IsDefined(typeof(ManagerAttribute), false)
            || parameters[1].ParameterType != eventParameters[^1].ParameterType
            || parameters.Any(parameter => parameter.ParameterType.IsByRef))
        {
            throw new InvalidOperationException($"Connection {method.DeclaringType?.FullName}.{method.Name} must be a synchronous static void method taking a [Manager] and the payload of {publisher.Name}.{name}, a public instance event with one payload (and optional object sender).");
        }
        return new Connection(publisher, item, method, parameters[0].ParameterType);
    }
}
