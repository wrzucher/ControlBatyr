using System.Reflection;
using ControlBatyr.Requirements;

namespace ControlBatyr.Inspection;

internal static class RequirementLinks
{
    internal static IEnumerable<(Type Manager, string Operation, MethodInfo Test)> Find(IEnumerable<Type> types) =>
        types.Distinct().SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .SelectMany(test => test.CustomAttributes.Where(attribute => attribute.AttributeType.IsGenericType
                && attribute.AttributeType.GetGenericTypeDefinition() == typeof(RequirementAttribute<>))
                .Select(attribute => (attribute.AttributeType.GenericTypeArguments[0],
                    (string)attribute.ConstructorArguments[0].Value!, test)));
}
