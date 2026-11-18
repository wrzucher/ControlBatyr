using System.Reflection;

namespace ControlBatyr.Inspection;

internal static class MetadataTypes
{
    internal static bool IsType(this Type? type, string fullName) => type?.FullName == fullName;

    internal static bool HasAttribute<T>(this MemberInfo member) where T : Attribute =>
        member.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == typeof(T).FullName);

    internal static IEnumerable<Type> PublisherManagers(this Type type) => type.CustomAttributes
        .Where(attribute => attribute.AttributeType.IsGenericType
            && attribute.AttributeType.GetGenericTypeDefinition().IsType("ControlBatyr.EventPublisherAttribute`1"))
        .Select(attribute => attribute.AttributeType.GetGenericArguments()[0]);

    internal static bool IsPublisher(this Type type) => type.PublisherManagers().Any();

    internal static Type? NullableUnderlyingType(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition().IsType("System.Nullable`1")
            ? type.GetGenericArguments()[0] : null;
}
