using System.Reflection;

namespace ControlBatyr.Inspection;

internal static class MetadataTypes
{
    internal static bool IsType(this Type? type, string fullName) => type?.FullName == fullName;

    internal static bool HasAttribute<T>(this MemberInfo member) where T : Attribute =>
        member.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == typeof(T).FullName);

    internal static Type? NullableUnderlyingType(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition().IsType("System.Nullable`1")
            ? type.GetGenericArguments()[0] : null;
}
