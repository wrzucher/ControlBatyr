using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace ControlBatyr.Inspection;

internal sealed class BusinessDocument
{
    private const BindingFlags PublicMembers = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    private readonly Type[] types;
    private readonly HashSet<Type> models = [];
    private readonly XmlComments comments = new();
    private readonly NullabilityInfoContext nullability = new();
    private readonly StringBuilder output = new();
    private readonly string? documentPath;

    internal BusinessDocument(Assembly[] assemblies, string? documentPath)
    {
        this.documentPath = documentPath;
        types = assemblies.Distinct().OrderBy(assembly => assembly.FullName, StringComparer.Ordinal)
            .SelectMany(assembly => assembly.GetTypes().OrderBy(type => type.MetadataToken))
            .Where(type => !Excluded(type)).ToArray();
        foreach (var type in types)
        {
            if (type.IsDefined(typeof(ObjectModelAttribute), false))
            {
                Discover(type);
            }
            if (type.IsDefined(typeof(ManagerAttribute), false))
            {
                DiscoverMethods(type);
            }
            if (type.IsDefined(typeof(EventPublisherAttribute), false))
            {
                foreach (var parameter in EventsOf(type).SelectMany(EventParameters))
                {
                    Discover(parameter.ParameterType);
                }
            }
        }
    }

    internal string Render()
    {
        Text("# Business API");
        Section("Managers", types.Where(type => type.IsDefined(typeof(ManagerAttribute), false)), Methods);
        Section("Events", types.Where(type => type.IsDefined(typeof(EventPublisherAttribute), false)), type =>
        {
            foreach (var item in EventsOf(type))
            {
                Text($"#### {item.Name}");
                Text(comments.Summary(item));
                var payload = EventPayload(item);
                if (payload.Length > 0)
                {
                    Text($"**Payload:** {payload}");
                }
                Remarks(item);
            }
        });
        Section("Models", models.OrderBy(type => type.Assembly.FullName, StringComparer.Ordinal)
            .ThenBy(type => type.MetadataToken), Model);
        return output.ToString().TrimEnd() + "\n";
    }

    private static bool Excluded(Type type) => type.IsDefined(typeof(PersistenceAttribute), false);
    private static bool Framework(Type type) => type.Assembly == typeof(string).Assembly
        || type.Assembly.GetName().Name?.StartsWith("System.", StringComparison.Ordinal) == true;

    private void Discover(Type type)
    {
        if (type.HasElementType)
        {
            Discover(type.GetElementType()!);
            return;
        }
        if (type == typeof(void) || type == typeof(CancellationToken) || type == typeof(Task) || type == typeof(ValueTask)
            || type.IsGenericParameter || Excluded(type))
        {
            return;
        }
        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                Discover(argument);
            }
            if (Framework(type))
            {
                return;
            }
            type = type.GetGenericTypeDefinition();
        }
        if (Framework(type) || !models.Add(type) || type.IsEnum)
        {
            return;
        }
        foreach (var member in DataOf(type))
        {
            Discover(MemberType(member));
        }
        DiscoverMethods(type);
    }

    private void DiscoverMethods(Type type)
    {
        foreach (var method in MethodsOf(type))
        {
            Discover(ReturnType(method.ReturnType));
            foreach (var parameter in ParametersOf(method))
            {
                Discover(parameter.ParameterType);
            }
        }
    }

    private static IEnumerable<MemberInfo> DataOf(Type type) => type.GetProperties(PublicMembers)
        .Where(property => property.GetMethod?.IsPublic == true && property.GetIndexParameters().Length == 0)
        .Cast<MemberInfo>().Concat(type.GetFields(PublicMembers)).OrderBy(member => member.MetadataToken);

    private static Type MemberType(MemberInfo member) => member is PropertyInfo property
        ? property.PropertyType : ((FieldInfo)member).FieldType;

    private static bool Computed(MemberInfo member) => member is PropertyInfo property
        && property.SetMethod is null && !property.GetMethod!.IsDefined(typeof(CompilerGeneratedAttribute), false);

    private void Model(Type type)
    {
        if (type.IsEnum)
        {
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .OrderBy(field => Convert.ToDecimal(field.GetRawConstantValue(), CultureInfo.InvariantCulture))
                .ThenBy(field => field.MetadataToken).ToArray();
            var described = fields.Any(field => comments.Summary(field).Length > 0);
            Line(described ? "| Value | Description |" : "| Value |");
            Line(described ? "| --- | --- |" : "| --- |");
            foreach (var field in fields)
            {
                var value = Convert.ToString(field.GetRawConstantValue(), CultureInfo.InvariantCulture);
                Line($"| {field.Name} = {value} |" + (described ? $" {comments.Summary(field)} |" : ""));
            }
            Line();
        }
        else
        {
            var members = DataOf(type).ToArray();
            Properties("Data", members.Where(member => !Computed(member)).ToArray());
            Properties("Computed properties", members.Where(Computed).ToArray());
            Methods(type);
        }
    }

    private void Properties(string title, MemberInfo[] members)
    {
        if (members.Length == 0)
        {
            return;
        }
        Text($"#### {title}");
        var described = members.Any(member => comments.Summary(member).Length > 0);
        Line(described ? "| Name | Type | Description |" : "| Name | Type |");
        Line(described ? "| --- | --- | --- |" : "| --- | --- |");
        foreach (var member in members)
        {
            var info = member is PropertyInfo property ? nullability.Create(property) : nullability.Create((FieldInfo)member);
            Line($"| {member.Name} | {TypeName(info.Type, info)} |" + (described ? $" {comments.Summary(member)} |" : ""));
        }
        Line();
    }

    private void Section(string title, IEnumerable<Type> source, Action<Type> body)
    {
        var members = source.ToArray();
        if (members.Length == 0)
        {
            return;
        }
        Text($"## {title}");
        foreach (var type in members)
        {
            Text($"<a id=\"{Anchor(type)}\"></a>");
            Text($"### {DisplayName(type)}");
            if (documentPath is not null && SourceFiles.Find(type) is { } sourcePath)
            {
                var relative = Path.GetRelativePath(Path.GetDirectoryName(Path.GetFullPath(documentPath))!, sourcePath);
                var url = string.Join("/", relative.Replace('\\', '/').Split('/').Select(Uri.EscapeDataString));
                Text($"[Source]({url})");
            }
            Text(comments.Summary(type));
            Remarks(type);
            body(type);
        }
    }

    private static IEnumerable<MethodInfo> MethodsOf(Type type) => type.GetMethods(PublicMembers)
        .Where(method => !method.IsSpecialName && method.DeclaringType != typeof(object)
            && !method.IsDefined(typeof(CompilerGeneratedAttribute), false))
        .OrderBy(method => method.MetadataToken);

    private static ParameterInfo[] ParametersOf(MethodInfo method) => method.GetParameters()
        .Where(parameter => parameter.ParameterType != typeof(CancellationToken)).ToArray();

    private static Type ReturnType(Type type) => type == typeof(Task) || type == typeof(ValueTask) ? typeof(void)
        : type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Task<>) || type.GetGenericTypeDefinition() == typeof(ValueTask<>))
            ? type.GetGenericArguments()[0] : type;

    private void Methods(Type type)
    {
        foreach (var method in MethodsOf(type))
        {
            var result = ReturnType(method.ReturnType);
            var info = nullability.Create(method.ReturnParameter);
            var name = method.Name;
            if (result != method.ReturnType)
            {
                if (name.EndsWith("Async", StringComparison.Ordinal))
                {
                    name = name[..^5];
                }
                info = info.GenericTypeArguments.FirstOrDefault() ?? info;
            }
            if (method.IsGenericMethod)
            {
                name += "&lt;" + string.Join(", ", method.GetGenericArguments().Select(argument => argument.Name)) + "&gt;";
            }
            Text($"#### {name}");
            Text(comments.Summary(method));
            var parameters = ParametersOf(method);
            if (parameters.Length > 0)
            {
                Text("**Parameters**");
                var described = parameters.Any(parameter => comments.Tag(method, "param", parameter.Name).Length > 0);
                Line(described ? "| Name | Type | Description |" : "| Name | Type |");
                Line(described ? "| --- | --- | --- |" : "| --- | --- |");
                foreach (var parameter in parameters)
                {
                    Line($"| {parameter.Name} | {TypeName(parameter.ParameterType, nullability.Create(parameter))} |"
                        + (described ? $" {comments.Tag(method, "param", parameter.Name)} |" : ""));
                }
                Line();
            }
            var returns = comments.Tag(method, "returns");
            if (result != typeof(void) || returns.Length > 0)
            {
                Text(result == typeof(void) ? "**Returns**" : $"**Returns:** {TypeName(result, info)}");
                Text(returns);
            }
            Remarks(method);
        }
    }

    private void Remarks(MemberInfo member)
    {
        var remarks = comments.Tag(member, "remarks");
        if (remarks.Length > 0)
        {
            Text("**Remarks**");
            Text(remarks);
        }
    }

    private static EventInfo[] EventsOf(Type type) => type.GetEvents(PublicMembers).OrderBy(item => item.MetadataToken).ToArray();
    private static ParameterInfo[] EventParameters(EventInfo item) => item.EventHandlerType!.GetMethod("Invoke")!.GetParameters();

    private string EventPayload(EventInfo item)
    {
        var handler = item.EventHandlerType!;
        var definition = handler.IsGenericType ? handler.GetGenericTypeDefinition() : handler;
        var declaredParameters = definition.GetMethod("Invoke")!.GetParameters();
        var handlerInfo = nullability.Create(item);
        return string.Join(", ", EventParameters(item).Select((parameter, index) =>
        {
            // Delegate annotations describe T?, while the event's type arguments describe the actual payload.
            var declaredType = declaredParameters[index].ParameterType;
            var info = declaredType.IsGenericParameter
                ? handlerInfo.GenericTypeArguments.ElementAtOrDefault(declaredType.GenericParameterPosition)
                : nullability.Create(parameter);
            return TypeName(parameter.ParameterType, info);
        }));
    }

    private string TypeName(Type type, NullabilityInfo? info = null)
    {
        if (type.IsByRef || type.IsPointer)
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
        var name = DisplayName(type);
        var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        if (models.Contains(definition))
        {
            name = $"[{name}](#{Anchor(definition)})";
        }
        if (type.IsGenericType)
        {
            name += "&lt;" + string.Join(", ", type.GetGenericArguments().Select((argument, index) =>
                TypeName(argument, info?.GenericTypeArguments.ElementAtOrDefault(index)))) + "&gt;";
        }
        return name + NullableSuffix(type, info);
    }

    private static string DisplayName(Type type) => type == typeof(string) ? "string"
        : type == typeof(bool) ? "bool" : type == typeof(decimal) ? "decimal"
        : type == typeof(int) ? "int" : type == typeof(long) ? "long"
        : type == typeof(short) ? "short" : type == typeof(byte) ? "byte"
        : type == typeof(uint) ? "uint" : type == typeof(ulong) ? "ulong"
        : type == typeof(ushort) ? "ushort" : type == typeof(sbyte) ? "sbyte"
        : type == typeof(float) ? "float" : type == typeof(double) ? "double"
        : type == typeof(char) ? "char" : type == typeof(object) ? "object" : type.Name.Split('`')[0];

    private static string NullableSuffix(Type type, NullabilityInfo? info) =>
        !type.IsValueType && info?.ReadState == NullabilityState.Nullable ? "?" : "";

    private static string Anchor(Type type) => "type-" + type.FullName!.Replace('.', '-').Replace('+', '-').Replace('`', '-').ToLowerInvariant();
    private void Text(string text)
    {
        if (text.Length > 0)
        {
            Line(text);
            Line();
        }
    }
    private void Line(string text = "") => output.Append(text).Append('\n');
}
