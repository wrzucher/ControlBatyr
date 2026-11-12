using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace ControlBatyr.Inspection;

/// <summary>Generates business documentation from role attributes and adjacent XML documentation files.</summary>
public static class BusinessApi
{
    /// <summary>Returns stable Markdown for the attributed core types in the supplied assemblies.</summary>
    public static string Generate(params Assembly[] assemblies) => new Document(assemblies).Render();

    private sealed class Document
    {
        private const BindingFlags PublicMembers = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
        private readonly Type[] types;
        private readonly HashSet<Type> models;
        private readonly XmlComments comments;
        private readonly NullabilityInfoContext nullability = new();
        private readonly StringBuilder output = new();

        internal Document(Assembly[] assemblies)
        {
            var inputs = assemblies.Distinct().OrderBy(assembly => assembly.FullName, StringComparer.Ordinal).ToArray();
            types = inputs.SelectMany(assembly => assembly.GetTypes().OrderBy(type => type.MetadataToken))
                .Where(type => !type.IsDefined(typeof(PersistenceAttribute), false))
                .ToArray();
            models = types.Where(type => type.IsDefined(typeof(ObjectModelAttribute), false)).ToHashSet();
            comments = new XmlComments(inputs);
        }

        internal string Render()
        {
            Line("# Business API");
            Line();
            Section<ManagerAttribute>("Managers", type =>
            {
                Methods(type);
                Enums(MethodsOf(type).SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType)
                    .Append(method.ReturnType)));
            });
            Section<ObjectModelAttribute>("Object models", type =>
            {
                var properties = type.GetProperties(PublicMembers)
                    .Where(property => property.GetMethod?.IsPublic == true && property.GetIndexParameters().Length == 0)
                    .OrderBy(property => property.MetadataToken).ToArray();
                var fields = type.GetFields(PublicMembers).OrderBy(field => field.MetadataToken).ToArray();
                Line("#### Data");
                Line();
                Line("| Name | Type | Description |");
                Line("| --- | --- | --- |");
                foreach (var member in properties.Where(IsData).Cast<MemberInfo>()
                    .Concat(fields).OrderBy(member => member.MetadataToken))
                {
                    var info = member is PropertyInfo property ? nullability.Create(property) : nullability.Create((FieldInfo)member);
                    Line($"| {member.Name} | {TypeName(info.Type, info)} | {comments.Summary(member)} |");
                }
                Line();
                var computed = properties.Where(property => !IsData(property)).ToArray();
                if (computed.Length > 0)
                {
                    Line("#### Computed properties");
                    Line();
                    Line("| Name | Type | Description |");
                    Line("| --- | --- | --- |");
                    foreach (var property in computed)
                    {
                        Line($"| {property.Name} | {TypeName(property.PropertyType, nullability.Create(property))} | {comments.Summary(property)} |");
                    }
                    Line();
                }
                Methods(type);
                Enums(properties.Select(property => property.PropertyType).Concat(fields.Select(field => field.FieldType))
                    .Concat(MethodsOf(type).SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType)
                        .Append(method.ReturnType))));
            });
            Section<EventPublisherAttribute>("Events", type =>
            {
                var events = type.GetEvents(PublicMembers).OrderBy(item => item.MetadataToken).ToArray();
                Line("| Event | Payload | Description |");
                Line("| --- | --- | --- |");
                foreach (var item in events)
                {
                    Line($"| {item.Name} | {EventPayload(item)} | {comments.Summary(item)} |");
                }
                Line();
                Enums(events.SelectMany(item => item.EventHandlerType?.GetMethod("Invoke")?.GetParameters()
                    .Select(parameter => parameter.ParameterType) ?? []));
            });
            return output.ToString().TrimEnd() + "\n";
        }

        private static bool IsData(PropertyInfo property) => property.SetMethod is not null
            || property.GetMethod!.IsDefined(typeof(CompilerGeneratedAttribute), false);

        private string EventPayload(EventInfo item)
        {
            var handler = item.EventHandlerType!;
            var parameters = handler.GetMethod("Invoke")!.GetParameters();
            var definition = handler.IsGenericType ? handler.GetGenericTypeDefinition() : handler;
            var declaredParameters = definition.GetMethod("Invoke")!.GetParameters();
            var handlerInfo = nullability.Create(item);
            return parameters.Length == 0 ? "—" : string.Join(", ", parameters.Select((parameter, index) =>
            {
                // Delegate parameters such as Action<T>.Invoke(T?) carry annotations from the delegate,
                // while the event's type arguments describe the actual payload's nullability.
                var declaredType = declaredParameters[index].ParameterType;
                var info = declaredType.IsGenericParameter
                    ? handlerInfo.GenericTypeArguments.ElementAtOrDefault(declaredType.GenericParameterPosition)
                    : nullability.Create(parameter);
                return TypeName(parameter.ParameterType, info);
            }));
        }

        private void Section<T>(string title, Action<Type> body) where T : Attribute
        {
            var members = types.Where(type => type.IsDefined(typeof(T), false)).ToArray();
            if (members.Length == 0)
            {
                return;
            }
            Line($"## {title}");
            Line();
            foreach (var type in members)
            {
                Line($"<a id=\"{Anchor(type)}\"></a>");
                Line();
                Line($"### {type.Name}");
                Line();
                Line(comments.Summary(type));
                Line();
                body(type);
            }
        }

        private static IEnumerable<MethodInfo> MethodsOf(Type type) => type.GetMethods(PublicMembers)
            .Where(method => !method.IsSpecialName && method.DeclaringType != typeof(object)
                && !method.IsDefined(typeof(CompilerGeneratedAttribute), false))
            .OrderBy(method => method.MetadataToken);

        private void Methods(Type type)
        {
            var methods = MethodsOf(type).ToArray();
            if (methods.Length == 0)
            {
                return;
            }
            Line("#### Methods");
            Line();
            Line("| Method | Description |");
            Line("| --- | --- |");
            foreach (var method in methods)
            {
                var name = method.Name;
                var result = method.ReturnType;
                var info = nullability.Create(method.ReturnParameter);
                var asynchronous = result == typeof(Task) || result == typeof(ValueTask)
                    || result.IsGenericType && (result.GetGenericTypeDefinition() == typeof(Task<>)
                        || result.GetGenericTypeDefinition() == typeof(ValueTask<>));
                if (asynchronous)
                {
                    if (name.EndsWith("Async", StringComparison.Ordinal))
                    {
                        name = name[..^5];
                    }
                    result = result.IsGenericType ? result.GetGenericArguments()[0] : typeof(void);
                    info = info.GenericTypeArguments.FirstOrDefault() ?? info;
                }
                if (method.IsGenericMethod)
                {
                    name += "&lt;" + string.Join(", ", method.GetGenericArguments().Select(argument => argument.Name)) + "&gt;";
                }
                var parameters = method.GetParameters().Where(parameter => parameter.ParameterType != typeof(CancellationToken))
                    .Select(parameter => $"{parameter.Name}: {TypeName(parameter.ParameterType, nullability.Create(parameter))}");
                Line($"| {name}({string.Join(", ", parameters)}) → {TypeName(result, info)} | {comments.Summary(method)} |");
            }
            Line();
        }

        private string TypeName(Type type, NullabilityInfo? info = null)
        {
            if (type.IsByRef)
            {
                return TypeName(type.GetElementType()!, info?.ElementType);
            }
            if (type.IsArray)
            {
                return TypeName(type.GetElementType()!, info?.ElementType)
                    + "[" + new string(',', type.GetArrayRank() - 1) + "]" + NullableSuffix(type, info);
            }
            if (Nullable.GetUnderlyingType(type) is { } underlying)
            {
                return TypeName(underlying, info?.GenericTypeArguments.FirstOrDefault()) + "?";
            }
            var name = type == typeof(void) ? "void" : type == typeof(string) ? "string"
                : type == typeof(bool) ? "bool" : type == typeof(decimal) ? "decimal"
                : type == typeof(int) ? "int" : type == typeof(long) ? "long"
                : type == typeof(short) ? "short" : type == typeof(byte) ? "byte"
                : type == typeof(uint) ? "uint" : type == typeof(ulong) ? "ulong"
                : type == typeof(ushort) ? "ushort" : type == typeof(sbyte) ? "sbyte"
                : type == typeof(float) ? "float" : type == typeof(double) ? "double"
                : type == typeof(char) ? "char" : type == typeof(object) ? "object" : type.Name.Split('`')[0];
            if (models.Contains(type.IsGenericType ? type.GetGenericTypeDefinition() : type))
            {
                name = $"[{name}](#{Anchor(type.IsGenericType ? type.GetGenericTypeDefinition() : type)})";
            }
            if (type.IsGenericType)
            {
                name += "&lt;" + string.Join(", ", type.GetGenericArguments().Select((argument, index) =>
                    TypeName(argument, info?.GenericTypeArguments.ElementAtOrDefault(index)))) + "&gt;";
            }
            return name + NullableSuffix(type, info);
        }

        private static string NullableSuffix(Type type, NullabilityInfo? info) =>
            !type.IsValueType && info?.ReadState == NullabilityState.Nullable ? "?" : "";

        private void Enums(IEnumerable<Type> usedTypes)
        {
            foreach (var type in usedTypes.SelectMany(ContainedTypes).Where(type => type.IsEnum).Distinct()
                .OrderBy(type => type.Assembly.FullName, StringComparer.Ordinal).ThenBy(type => type.MetadataToken))
            {
                Line($"#### {type.Name}");
                Line();
                Line(comments.Summary(type));
                Line();
                Line("| Value | Description |");
                Line("| --- | --- |");
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static)
                    .OrderBy(field => Convert.ToDecimal(field.GetRawConstantValue(), CultureInfo.InvariantCulture))
                    .ThenBy(field => field.MetadataToken))
                {
                    Line($"| {field.Name} = {Convert.ToString(field.GetRawConstantValue(), CultureInfo.InvariantCulture)} | {comments.Summary(field)} |");
                }
                Line();
            }
        }

        private static IEnumerable<Type> ContainedTypes(Type type) => new[] { type }
            .Concat(type.HasElementType ? ContainedTypes(type.GetElementType()!) : [])
            .Concat(type.GetGenericArguments().SelectMany(ContainedTypes));

        private static string Anchor(Type type) => "type-" + type.FullName!.Replace('.', '-').Replace('+', '-').Replace('`', '-').ToLowerInvariant();
        private void Line(string text = "") => output.Append(text).Append('\n');
    }
}
