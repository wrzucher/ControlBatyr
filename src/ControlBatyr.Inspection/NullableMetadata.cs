using System.Reflection;

namespace ControlBatyr.Inspection;

internal sealed record NullableMetadata(Type Type, NullabilityState ReadState,
    NullableMetadata? ElementType, NullableMetadata[] GenericTypeArguments)
{
    internal static NullableMetadata Create(PropertyInfo property) => Read(property, property.CustomAttributes, new NullabilityInfoContext().Create(property));
    internal static NullableMetadata Create(FieldInfo field) => Read(field, field.CustomAttributes, new NullabilityInfoContext().Create(field));
    internal static NullableMetadata Create(EventInfo item) => Read(item, item.CustomAttributes, new NullabilityInfoContext().Create(item));
    internal static NullableMetadata Create(ParameterInfo parameter) => Read(parameter.Member, parameter.CustomAttributes, new NullabilityInfoContext().Create(parameter));

    private static NullableMetadata Read(MemberInfo member, IEnumerable<CustomAttributeData> attributes, NullabilityInfo info)
    {
        if (!ContainsNullableValue(info.Type)) return Copy(info);
        // NullabilityInfoContext compares Nullable<T> with a runtime type, shifting nested metadata annotations.
        var annotation = attributes.FirstOrDefault(attribute => attribute.AttributeType.IsType("System.Runtime.CompilerServices.NullableAttribute"));
        var value = annotation?.ConstructorArguments[0].Value;
        var flags = value is IEnumerable<CustomAttributeTypedArgument> items
            ? items.Select(item => (byte)item.Value!).ToArray() : null;
        byte? context = null;
        for (var scope = member; scope is not null; scope = scope.DeclaringType)
        {
            var attribute = scope.CustomAttributes.FirstOrDefault(item => item.AttributeType.IsType("System.Runtime.CompilerServices.NullableContextAttribute"));
            if (attribute is null) continue;
            context = (byte)attribute.ConstructorArguments[0].Value!;
            break;
        }
        var index = 0;
        return Parse(info.Type, info) with { ReadState = info.ReadState };

        NullableMetadata Parse(Type type, NullabilityInfo? fallback)
        {
            var underlying = type.IsByRef || type.IsPointer ? type.GetElementType()! : type;
            underlying = MetadataTypes.NullableUnderlyingType(underlying) ?? underlying;
            var state = fallback?.ReadState ?? NullabilityState.Unknown;
            if (!underlying.IsValueType)
            {
                var flag = value is byte single ? single : flags is not null && index < flags.Length ? flags[index] : context;
                index++;
                if (flag is not null) state = flag == 2 ? NullabilityState.Nullable : flag == 1 ? NullabilityState.NotNull : NullabilityState.Unknown;
            }
            else if (underlying.IsGenericType) index++;
            var element = underlying.IsArray ? Parse(underlying.GetElementType()!, fallback?.ElementType) : null;
            var arguments = underlying.IsGenericType ? underlying.GetGenericArguments()
                .Select((argument, position) => Parse(argument, fallback?.GenericTypeArguments.ElementAtOrDefault(position))).ToArray() : [];
            return new(type, state, element, arguments);
        }
    }

    private static bool ContainsNullableValue(Type type) => MetadataTypes.NullableUnderlyingType(type) is not null
        || (type.HasElementType && ContainsNullableValue(type.GetElementType()!))
        || (type.IsGenericType && type.GetGenericArguments().Any(ContainsNullableValue));

    private static NullableMetadata Copy(NullabilityInfo info) => new(info.Type, info.ReadState,
        info.ElementType is null ? null : Copy(info.ElementType), info.GenericTypeArguments.Select(Copy).ToArray());
}
