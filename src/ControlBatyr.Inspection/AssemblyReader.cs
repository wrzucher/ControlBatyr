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
    private readonly string? documentDirectory;
    private readonly Domain[] domains;
    private readonly Connection[] connections;
    private readonly HashSet<Type> suppliedPublishers;
    private readonly (Type Manager, string Operation, MethodInfo Test)[] requirements;

    private sealed record Domain(Type Manager, Type[] Publishers, Type[] RecordEvents, HashSet<Type> Models)
    {
        internal string Name => Manager.Name.EndsWith("Manager", StringComparison.Ordinal) ? Manager.Name[..^7] : Manager.Name;
    }

    internal AssemblyReader(Assembly[] assemblies, string? documentDirectory)
    {
        this.documentDirectory = documentDirectory;
        var types = assemblies.Distinct().OrderBy(assembly => assembly.FullName, StringComparer.Ordinal)
            .SelectMany(assembly => assembly.GetTypes().OrderBy(type => type.MetadataToken))
            .Where(type => !Excluded(type)).ToArray();
        requirements = RequirementLinks.Find(types).ToArray();
        suppliedPublishers = types.Where(type => type.HasAttribute<EventPublisherAttribute>()).ToHashSet();
        domains = types.Where(type => type.HasAttribute<ManagerAttribute>()).Select(manager =>
        {
            models.Clear();
            DiscoverMethods(manager);
            var publishers = manager.GetConstructors().SelectMany(constructor => constructor.GetParameters())
                .Select(parameter => parameter.ParameterType)
                .Where(type => type.HasAttribute<EventPublisherAttribute>()).Distinct().ToArray();
            foreach (var parameter in publishers.SelectMany(EventsOf).SelectMany(EventParameters))
            {
                Discover(parameter.ParameterType);
            }
            var recordEvents = types.Where(type => EventManagers(type).Contains(manager)).ToArray();
            foreach (var item in recordEvents)
            {
                if (EventManagers(item).Count() != 1)
                {
                    throw new InvalidOperationException($"Event {item.FullName} must have exactly one [Event<TManager>] domain in the supplied assemblies.");
                }
                Discover(item);
            }
            return new Domain(manager, publishers, recordEvents, [.. models]);
        }).ToArray();
        models.Clear();
        models.UnionWith(domains.SelectMany(domain => domain.Models));
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
            Node(connection.Method),
            Id(connection.Event), Id(connection.Manager))).ToImmutableArray();
        return new(domainNodes, modelNodes, connectionNodes);
    }

    private BusinessDomain DomainModel(Domain domain)
    {
        var publishers = domain.Publishers.Select(publisher => new BusinessPublisher(Node(publisher),
            [.. EventsOf(publisher).Select(item => new BusinessEvent(Node(item), Payloads(item)))]));
        return new(domain.Name, Node(domain.Manager), Operations(domain.Manager), [.. publishers],
            [.. domain.RecordEvents.Select(type => new BusinessEvent(Node(type), [Reference(type)]))]);
    }

    private BusinessNode Node(MemberInfo member)
    {
        var source = documentDirectory is null ? null : SourceFiles.Find(member);
        return new(Id(member), member is Type item ? DisplayName(item) : member.Name,
            comments.Summary(member), comments.Tag(member, "remarks"), source is null ? null
                : Path.GetRelativePath(Path.GetFullPath(documentDirectory!), source).Replace('\\', '/'));
    }

    private static string Id(MemberInfo member)
    {
        return "A:" + member.Module.Assembly.GetName().Name + "|" + XmlComments.Id(member);
    }

    private BusinessModel Model(Type type)
    {
        var node = Node(type);
        if (type.IsEnum)
        {
            var values = type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .OrderBy(field => Convert.ToDecimal(field.GetRawConstantValue(), CultureInfo.InvariantCulture))
                .ThenBy(field => field.MetadataToken).Select(field => new BusinessEnumValue(Node(field),
                    Convert.ToString(field.GetRawConstantValue(), CultureInfo.InvariantCulture)!)).ToImmutableArray();
            return new(node, true, [], [], values);
        }
        var properties = DataOf(type).Select(member =>
        {
            var info = member is PropertyInfo property ? NullableMetadata.Create(property) : NullableMetadata.Create((FieldInfo)member);
            return new BusinessProperty(Node(member), Reference(info.Type, info, TupleNames(member)), Computed(member));
        }).ToImmutableArray();
        return new(node, false, properties, Operations(type), [])
        {
            IsRecord = type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance) is not null
                || type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).SingleOrDefault(method => method.Name == "PrintMembers"
                    && method.GetParameters() is [var parameter] && parameter.ParameterType.IsType("System.Text.StringBuilder"))
                    ?.HasAttribute<CompilerGeneratedAttribute>() == true
        };
    }

    private ImmutableArray<BusinessOperation> Operations(Type type)
    {
        return [.. MethodsOf(type).Select(method => Operation(type, method))];
    }

    private BusinessOperation Operation(Type owner, MethodInfo method)
    {
        var result = ReturnType(method.ReturnType);
        var info = NullableMetadata.Create(method.ReturnParameter);
        var node = Node(method);
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
            Reference(parameter.ParameterType, NullableMetadata.Create(parameter), TupleNames(parameter)), comments.Tag(method, "param", parameter.Name)));
        var tests = requirements.Where(item => owner.HasAttribute<ManagerAttribute>() && item.Manager == owner && item.Operation == method.Name)
            .Select(item => new BusinessRequirement(Node(item.Test)));
        return new(node with { Name = name }, [.. parameters], Reference(result, info, TupleNames(method.ReturnParameter)),
            comments.Tag(method, "returns"), [.. tests], RaisedEvents(method));
    }

    private ImmutableArray<string> RaisedEvents(MethodInfo method)
    {
        return [.. method.CustomAttributes
            .Where(attribute => attribute.AttributeType.IsGenericType
                && attribute.AttributeType.GetGenericTypeDefinition().IsType("ControlBatyr.RaisesAttribute`1"))
            .Select(attribute =>
            {
                var publisher = attribute.AttributeType.GetGenericArguments()[0];
                var recordManagers = EventManagers(publisher).ToArray();
                if (attribute.ConstructorArguments.Count == 0)
                {
                    if (recordManagers.Length != 1 || domains.Count(domain => domain.RecordEvents.Contains(publisher)) != 1)
                    {
                        throw new InvalidOperationException($"Operation {method.DeclaringType?.FullName}.{method.Name} raises {publisher.Name}, which must be an [Event<TManager>] type of exactly one domain in the supplied assemblies; an [EventPublisher] requires an event name.");
                    }
                    return Id(publisher);
                }
                if (recordManagers.Length > 0)
                {
                    throw new InvalidOperationException($"Operation {method.DeclaringType?.FullName}.{method.Name} raises record event {publisher.Name}, which must be used without an event name.");
                }
                var name = (string?)attribute.ConstructorArguments[0].Value;
                if (name is null)
                {
                    throw new InvalidOperationException($"Operation {method.DeclaringType?.FullName}.{method.Name} raises {publisher.Name}, which requires an event name.");
                }
                var item = publisher.GetEvent(name);
                if (!suppliedPublishers.Contains(publisher) || item?.AddMethod?.IsStatic != false
                    || domains.Count(domain => domain.Publishers.Contains(publisher)) != 1)
                {
                    throw new InvalidOperationException($"Operation {method.DeclaringType?.FullName}.{method.Name} raises {publisher.Name}.{name}, which must be a public instance event on an [EventPublisher] with one publisher domain in the supplied assemblies.");
                }
                return Id(item);
            }).Distinct()];
    }

    private static IEnumerable<Type> EventManagers(Type type) => type.CustomAttributes
        .Where(attribute => attribute.AttributeType.IsGenericType
            && attribute.AttributeType.GetGenericTypeDefinition().IsType("ControlBatyr.EventAttribute`1"))
        .Select(attribute => attribute.AttributeType.GetGenericArguments()[0]);

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
        var handlerInfo = NullableMetadata.Create(item);
        return [.. EventParameters(item).Select((parameter, index) =>
        {
            // Delegate annotations describe T?, while the event's arguments describe the actual payload.
            var declaredType = declared[index].ParameterType;
            var info = declaredType.IsGenericParameter
                ? handlerInfo.GenericTypeArguments.ElementAtOrDefault(declaredType.GenericParameterPosition)
                : NullableMetadata.Create(parameter);
            return Reference(parameter.ParameterType, info);
        })];
    }

    private static Queue<string?>? TupleNames(ICustomAttributeProvider provider)
    {
        var attributes = provider switch
        {
            MemberInfo member => member.CustomAttributes,
            ParameterInfo parameter => parameter.CustomAttributes,
            _ => []
        };
        var attribute = attributes.SingleOrDefault(item => item.AttributeType.IsType("System.Runtime.CompilerServices.TupleElementNamesAttribute"));
        return attribute is null ? null : new(((IEnumerable<CustomAttributeTypedArgument>)attribute.ConstructorArguments[0].Value!)
            .Select(item => (string?)item.Value));
    }

    private TypeReference Reference(Type type, NullableMetadata? info = null, Queue<string?>? tupleNames = null)
    {
        if (type.IsByRef || type.IsPointer)
        {
            return Reference(type.GetElementType()!, info?.ElementType, tupleNames);
        }
        if (type.IsArray)
        {
            return new("", null, [Reference(type.GetElementType()!, info?.ElementType, tupleNames)],
                "[" + new string(',', type.GetArrayRank() - 1) + "]" + NullableSuffix(type, info));
        }
        if (MetadataTypes.NullableUnderlyingType(type) is { } underlying)
        {
            return new("", null, [Reference(underlying, info, tupleNames)], "?");
        }
        var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        if (type.IsGenericType && definition.FullName!.StartsWith("System.ValueTuple`", StringComparison.Ordinal))
        {
            var types = type.GetGenericArguments();
            var count = TupleElementCount(type);
            var names = Enumerable.Range(0, count).Select(_ => tupleNames?.Count > 0 ? tupleNames.Dequeue() : null).ToArray();
            var elements = types.Select((argument, index) => Reference(argument,
                info?.GenericTypeArguments.ElementAtOrDefault(index), tupleNames)).ToArray();
            // The eighth CLR argument holds the remaining elements of a long tuple.
            var flattened = types.Length == 8 ? elements.Take(7).Concat(elements[7].Arguments) : elements.AsEnumerable();
            return new("(", null, [.. flattened.Select((element, index) => names[index] is { } name
                ? new TypeReference("", null, [element], " " + name) : element)], "");
        }
        return new(type.IsType("System.Void") ? "void" : DisplayName(type), models.Contains(definition) ? Id(definition) : null,
            type.IsGenericType ? [.. type.GetGenericArguments().Select((argument, index) =>
                Reference(argument, info?.GenericTypeArguments.ElementAtOrDefault(index), tupleNames))] : [], NullableSuffix(type, info));
    }

    private static int TupleElementCount(Type type)
    {
        var arguments = type.GetGenericArguments();
        return arguments.Length == 8 ? 7 + TupleElementCount(arguments[7]) : arguments.Length;
    }

    private static bool Excluded(Type type)
    {
        return type.HasAttribute<PersistenceAttribute>();
    }
    private static bool Framework(Type type)
    {
        return type.Assembly.GetType("System.Object") is not null
        || type.Assembly.GetName().Name?.StartsWith("System.", StringComparison.Ordinal) == true;
    }

    private void Discover(Type type)
    {
        if (type.HasElementType)
        {
            Discover(type.GetElementType()!);
            return;
        }
        if (type.IsType("System.Void") || type.IsType("System.Threading.CancellationToken") || type.IsType("System.Threading.Tasks.Task") || type.IsType("System.Threading.Tasks.ValueTask")
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
            && property.SetMethod is null && !property.GetMethod!.HasAttribute<CompilerGeneratedAttribute>();
    }

    internal static IEnumerable<MethodInfo> MethodsOf(Type type)
    {
        var equalityTypes = type.GetInterfaces()
            .Where(item => item.IsGenericType && item.GetGenericTypeDefinition().IsType("System.IEquatable`1"))
            .Select(item => item.GetGenericArguments()[0]).ToHashSet();
        return type.GetMethods(PublicMembers)
            .Where(method => !method.IsSpecialName && !ObjectMethod(method)
                && !(method.Name == "Equals" && !method.IsStatic && !method.IsGenericMethod
                    && method.ReturnType.IsType("System.Boolean") && method.GetParameters() is [var parameter]
                    && equalityTypes.Contains(parameter.ParameterType))
                && !method.HasAttribute<CompilerGeneratedAttribute>())
            .OrderBy(method => method.MetadataToken);
    }

    private static bool ObjectMethod(MethodInfo method)
    {
        if (method.DeclaringType.IsType("System.Object")) return true;
        if (!method.IsVirtual || (method.Attributes & MethodAttributes.NewSlot) != 0) return false;
        var parameters = method.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
        for (var parent = method.DeclaringType?.BaseType; parent is not null; parent = parent.BaseType)
        {
            var overridden = parent.GetMethods(PublicMembers | BindingFlags.DeclaredOnly)
                .FirstOrDefault(candidate => candidate.IsVirtual && candidate.Name == method.Name
                    && candidate.GetGenericArguments().Length == method.GetGenericArguments().Length
                    && candidate.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(parameters));
            if (overridden is not null) return ObjectMethod(overridden);
        }
        return false;
    }

    private static ParameterInfo[] ParametersOf(MethodInfo method)
    {
        return method.GetParameters()
            .Where(parameter => !parameter.ParameterType.IsType("System.Threading.CancellationToken")).ToArray();
    }

    private static Type ReturnType(Type type)
    {
        return type.IsType("System.Threading.Tasks.Task") || type.IsType("System.Threading.Tasks.ValueTask") ? type.Assembly.GetType("System.Void", throwOnError: true)!
            : type.IsGenericType && (type.GetGenericTypeDefinition().IsType("System.Threading.Tasks.Task`1") || type.GetGenericTypeDefinition().IsType("System.Threading.Tasks.ValueTask`1"))
                ? type.GetGenericArguments()[0] : type;
    }

    private static string DisplayName(Type type)
    {
        return type.IsType("System.String") ? "string"
            : type.IsType("System.Boolean") ? "bool" : type.IsType("System.Decimal") ? "decimal"
            : type.IsType("System.Int32") ? "int" : type.IsType("System.Int64") ? "long"
            : type.IsType("System.Int16") ? "short" : type.IsType("System.Byte") ? "byte"
            : type.IsType("System.UInt32") ? "uint" : type.IsType("System.UInt64") ? "ulong"
            : type.IsType("System.UInt16") ? "ushort" : type.IsType("System.SByte") ? "sbyte"
            : type.IsType("System.Single") ? "float" : type.IsType("System.Double") ? "double"
            : type.IsType("System.Char") ? "char" : type.IsType("System.Object") ? "object" : type.Name.Split('`')[0];
    }

    private static string NullableSuffix(Type type, NullableMetadata? info)
    {
        return !type.IsValueType && info?.ReadState == NullabilityState.Nullable ? "?" : "";
    }
}
