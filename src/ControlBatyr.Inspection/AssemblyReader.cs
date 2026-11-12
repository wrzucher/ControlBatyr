using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ControlBatyr.Inspection;

internal sealed class AssemblyReader
{
    private const BindingFlags PublicMembers = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    private readonly HashSet<Type> models = [];
    private readonly XmlComments comments = new();
    private readonly NullabilityInfoContext nullability = new();
    private readonly string? documentDirectory;
    private readonly Dictionary<Type, string> modelFiles = [];
    private readonly Domain[] domains;
    private readonly Connection[] connections;
    private readonly (Type Manager, string Operation, MethodInfo Test)[] requirements;
    private readonly List<DocumentLocation> locations = [];

    private sealed record Domain(Type Manager, Type[] Publishers, HashSet<Type> Models)
    {
        internal string Name => Manager.Name.EndsWith("Manager", StringComparison.Ordinal) ? Manager.Name[..^7] : Manager.Name;
        internal string File => Name.ToLowerInvariant() + ".md";
    }

    internal AssemblyReader(Assembly[] assemblies, string? documentDirectory)
    {
        this.documentDirectory = documentDirectory;
        var types = assemblies.Distinct().OrderBy(assembly => assembly.FullName, StringComparer.Ordinal)
            .SelectMany(assembly => assembly.GetTypes().OrderBy(type => type.MetadataToken))
            .Where(type => !Excluded(type)).ToArray();
        requirements = RequirementLinks.Find(types).ToArray();
        domains = types.Where(type => type.IsDefined(typeof(ManagerAttribute), false)).Select(manager =>
        {
            models.Clear();
            DiscoverMethods(manager);
            var publishers = manager.GetConstructors().SelectMany(constructor => constructor.GetParameters())
                .Select(parameter => parameter.ParameterType)
                .Where(type => type.IsDefined(typeof(EventPublisherAttribute), false)).Distinct().ToArray();
            foreach (var parameter in publishers.SelectMany(EventsOf).SelectMany(EventParameters))
            {
                Discover(parameter.ParameterType);
            }
            return new Domain(manager, publishers, [.. models]);
        }).ToArray();
        var duplicate = domains.GroupBy(domain => domain.File).FirstOrDefault(group => group.Count() > 1
            || group.Key is "index.md" or "shared.md");
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Domain document path is not unique: {duplicate.Key}.");
        }
        models.Clear();
        models.UnionWith(domains.SelectMany(domain => domain.Models));
        foreach (var model in models)
        {
            var owners = domains.Where(domain => domain.Models.Contains(model)).ToArray();
            modelFiles[model] = owners.Length == 1 ? owners[0].File : "shared.md";
        }
        connections = Connection.Find(assemblies);
        foreach (var connection in connections)
        {
            if (domains.Count(domain => domain.Publishers.Contains(connection.Publisher)) != 1
                || domains.Count(domain => domain.Manager == connection.Manager) != 1)
            {
                throw new InvalidOperationException($"Connection {connection.Method.Name} needs one publisher domain and one target manager domain in the supplied assemblies.");
            }
        }
    }

    internal BusinessApiModel Read()
    {
        var domainNodes = domains.Select(DomainModel).ToImmutableArray();
        var modelNodes = models.OrderBy(type => type.Assembly.FullName, StringComparer.Ordinal)
            .ThenBy(type => type.MetadataToken).Select(Model).ToImmutableArray();
        var connectionNodes = connections.Select(connection => new BusinessConnection(
            Node(connection.Method, domains.Single(domain => domain.Manager == connection.Manager).File, Anchor(connection.Manager)),
            Id(connection.Event), Id(connection.Manager))).ToImmutableArray();
        return new(domainNodes, modelNodes, connectionNodes,
            [.. locations.DistinctBy(location => location.Id).OrderBy(location => location.Id, StringComparer.Ordinal)]);
    }

    private BusinessDomain DomainModel(Domain domain)
    {
        var publishers = domain.Publishers.Select(publisher => new BusinessPublisher(Node(publisher, domain.File),
            [.. EventsOf(publisher).Select(item => new BusinessEvent(Node(item, domain.File), Payloads(item)))]));
        return new(domain.Name, Node(domain.Manager, domain.File), Operations(domain.Manager, domain.File), [.. publishers]);
    }

    private BusinessNode Node(MemberInfo member, string file, string? anchor = null)
    {
        anchor ??= MemberAnchor(member);
        locations.Add(new(Id(member), file, anchor));
        var source = documentDirectory is null ? null : SourceFiles.Find(member);
        return new(Id(member), member is Type item ? DisplayName(item) : member.Name,
            comments.Summary(member), comments.Tag(member, "remarks"), source is null ? null
                : Path.GetRelativePath(Path.GetFullPath(documentDirectory!), source).Replace('\\', '/'));
    }

    private static string MemberAnchor(MemberInfo member)
    {
        if (member is Type type)
        {
            return Anchor(type);
        }
        var anchor = Anchor(member.DeclaringType!) + "-" + member.Name.ToLowerInvariant();
        if (member is MethodInfo method && MethodsOf(method.DeclaringType!).Count(item => item.Name == method.Name) > 1)
        {
            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Id(member)));
            anchor += "-" + Convert.ToHexStringLower(hash)[..8];
        }
        return anchor;
    }

    private static string Id(MemberInfo member)
    {
        return "A:" + member.Module.Assembly.GetName().Name + "|" + XmlComments.Id(member);
    }

    private BusinessModel Model(Type type)
    {
        var file = modelFiles[type];
        var node = Node(type, file);
        if (type.IsEnum)
        {
            var values = type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .OrderBy(field => Convert.ToDecimal(field.GetRawConstantValue(), CultureInfo.InvariantCulture))
                .ThenBy(field => field.MetadataToken).Select(field => new BusinessEnumValue(Node(field, file, Anchor(type)),
                    Convert.ToString(field.GetRawConstantValue(), CultureInfo.InvariantCulture)!)).ToImmutableArray();
            return new(node, true, [], [], values);
        }
        var properties = DataOf(type).Select(member =>
        {
            var info = member is PropertyInfo property ? nullability.Create(property) : nullability.Create((FieldInfo)member);
            return new BusinessProperty(Node(member, file, Anchor(type)), Reference(info.Type, info), Computed(member));
        }).ToImmutableArray();
        return new(node, false, properties, Operations(type, file), []);
    }

    private ImmutableArray<BusinessOperation> Operations(Type type, string file)
    {
        return [.. MethodsOf(type).Select(method => Operation(type, method, file))];
    }

    private BusinessOperation Operation(Type owner, MethodInfo method, string file)
    {
        var result = ReturnType(method.ReturnType);
        var info = nullability.Create(method.ReturnParameter);
        var node = Node(method, file);
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
        var parameters = ParametersOf(method).Select(parameter => new BusinessParameter(parameter.Name!,
            Reference(parameter.ParameterType, nullability.Create(parameter)), comments.Tag(method, "param", parameter.Name)));
        var tests = requirements.Where(item => owner.IsDefined(typeof(ManagerAttribute), false) && item.Manager == owner && item.Operation == method.Name)
            .Select(item => new BusinessRequirement(Node(item.Test, file, MemberAnchor(method))));
        return new(node with { Name = name }, [.. parameters], Reference(result, info), comments.Tag(method, "returns"), [.. tests]);
    }

    private static EventInfo[] EventsOf(Type type)
    {
        return type.GetEvents(PublicMembers).OrderBy(item => item.MetadataToken).ToArray();
    }
    private static ParameterInfo[] EventParameters(EventInfo item)
    {
        return item.EventHandlerType!.GetMethod("Invoke")!.GetParameters();
    }

    private ImmutableArray<TypeReference> Payloads(EventInfo item)
    {
        var handler = item.EventHandlerType!;
        var definition = handler.IsGenericType ? handler.GetGenericTypeDefinition() : handler;
        var declared = definition.GetMethod("Invoke")!.GetParameters();
        var handlerInfo = nullability.Create(item);
        return [.. EventParameters(item).Select((parameter, index) =>
        {
            // Delegate annotations describe T?, while the event's arguments describe the actual payload.
            var declaredType = declared[index].ParameterType;
            var info = declaredType.IsGenericParameter
                ? handlerInfo.GenericTypeArguments.ElementAtOrDefault(declaredType.GenericParameterPosition)
                : nullability.Create(parameter);
            return Reference(parameter.ParameterType, info);
        })];
    }

    private TypeReference Reference(Type type, NullabilityInfo? info = null)
    {
        if (type.IsByRef || type.IsPointer)
        {
            return Reference(type.GetElementType()!, info?.ElementType);
        }
        if (type.IsArray)
        {
            return new("", null, [Reference(type.GetElementType()!, info?.ElementType)],
                "[" + new string(',', type.GetArrayRank() - 1) + "]" + NullableSuffix(type, info));
        }
        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            return new("", null, [Reference(underlying, info?.GenericTypeArguments.FirstOrDefault())], "?");
        }
        var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        return new(type == typeof(void) ? "void" : DisplayName(type), models.Contains(definition) ? Id(definition) : null,
            type.IsGenericType ? [.. type.GetGenericArguments().Select((argument, index) =>
                Reference(argument, info?.GenericTypeArguments.ElementAtOrDefault(index)))] : [], NullableSuffix(type, info));
    }

    private static bool Excluded(Type type)
    {
        return type.IsDefined(typeof(PersistenceAttribute), false);
    }
    private static bool Framework(Type type)
    {
        return type.Assembly == typeof(string).Assembly
        || type.Assembly.GetName().Name?.StartsWith("System.", StringComparison.Ordinal) == true;
    }

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

    private static IEnumerable<MemberInfo> DataOf(Type type)
    {
        return type.GetProperties(PublicMembers)
            .Where(property => property.GetMethod?.IsPublic == true && property.GetIndexParameters().Length == 0)
            .Cast<MemberInfo>().Concat(type.GetFields(PublicMembers)).OrderBy(member => member.MetadataToken);
    }

    private static Type MemberType(MemberInfo member)
    {
        return member is PropertyInfo property
            ? property.PropertyType : ((FieldInfo)member).FieldType;
    }

    private static bool Computed(MemberInfo member)
    {
        return member is PropertyInfo property
            && property.SetMethod is null && !property.GetMethod!.IsDefined(typeof(CompilerGeneratedAttribute), false);
    }

    internal static IEnumerable<MethodInfo> MethodsOf(Type type)
    {
        return type.GetMethods(PublicMembers)
            .Where(method => !method.IsSpecialName && method.DeclaringType != typeof(object)
                && !method.IsDefined(typeof(CompilerGeneratedAttribute), false))
            .OrderBy(method => method.MetadataToken);
    }

    private static ParameterInfo[] ParametersOf(MethodInfo method)
    {
        return method.GetParameters()
            .Where(parameter => parameter.ParameterType != typeof(CancellationToken)).ToArray();
    }

    private static Type ReturnType(Type type)
    {
        return type == typeof(Task) || type == typeof(ValueTask) ? typeof(void)
            : type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Task<>) || type.GetGenericTypeDefinition() == typeof(ValueTask<>))
                ? type.GetGenericArguments()[0] : type;
    }

    private static string DisplayName(Type type)
    {
        return type == typeof(string) ? "string"
            : type == typeof(bool) ? "bool" : type == typeof(decimal) ? "decimal"
            : type == typeof(int) ? "int" : type == typeof(long) ? "long"
            : type == typeof(short) ? "short" : type == typeof(byte) ? "byte"
            : type == typeof(uint) ? "uint" : type == typeof(ulong) ? "ulong"
            : type == typeof(ushort) ? "ushort" : type == typeof(sbyte) ? "sbyte"
            : type == typeof(float) ? "float" : type == typeof(double) ? "double"
            : type == typeof(char) ? "char" : type == typeof(object) ? "object" : type.Name.Split('`')[0];
    }

    private static string NullableSuffix(Type type, NullabilityInfo? info)
    {
        return !type.IsValueType && info?.ReadState == NullabilityState.Nullable ? "?" : "";
    }

    private static string Anchor(Type type)
    {
        return "type-" + type.FullName!.Replace('.', '-').Replace('+', '-').Replace('`', '-').ToLowerInvariant();
    }
}
