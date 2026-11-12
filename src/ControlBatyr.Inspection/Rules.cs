using System.Reflection;
using System.Runtime.CompilerServices;

namespace ControlBatyr.Inspection;

internal static class Rules
{
    private const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;

    internal static Type[] ProductTypes(params Assembly[] assemblies)
    {
        var found = new HashSet<Assembly>(assemblies);
        var pending = new Queue<Assembly>(assemblies);
        while (pending.TryDequeue(out var assembly))
        {
            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                var dependency = Assembly.Load(reference);
                if (dependency.GetReferencedAssemblies().Any(name => name.Name == typeof(ManagerAttribute).Assembly.GetName().Name
                        || name.Name == typeof(ControlBatyr.Requirements.RequirementAttribute<>).Assembly.GetName().Name)
                    && found.Add(dependency))
                {
                    pending.Enqueue(dependency);
                }
            }
        }
        return found.SelectMany(assembly => assembly.GetTypes()).ToArray();
    }

    internal static IReadOnlyList<string> Check(params Type[] types)
    {
        var errors = new List<string>();
        var comments = new XmlComments();
        var managers = types.Distinct().Where(Role<ManagerAttribute>).ToArray();
        foreach (var manager in managers)
        {
            Dependencies(manager);
            Inputs(manager);
            foreach (var method in Methods(manager))
            {
                Summary(method);
            }
        }
        foreach (var type in types.Distinct())
        {
            if (Role<PersistenceAttribute>(type)) Persistence(type);
            if (Role<EventPublisherAttribute>(type)) Publisher(type);
            if (Role<ObjectModelAttribute>(type)) Immutable(type);
        }
        foreach (var requirement in RequirementLinks.Find(types))
        {
            if (!Role<ManagerAttribute>(requirement.Manager)
                || requirement.Manager.IsDefined(typeof(PersistenceAttribute), false)
                || !BusinessDocument.MethodsOf(requirement.Manager).Any(method => method.Name == requirement.Operation))
            {
                Error("CB0007", requirement.Test,
                    $"requirement targets {requirement.Manager.FullName}.{requirement.Operation}: select a public operation of a [Manager].");
            }
        }
        return errors;

        void Dependencies(Type manager)
        {
            foreach (var constructor in manager.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                foreach (var parameter in constructor.GetParameters())
                {
                    if (Role<ManagerAttribute>(parameter.ParameterType) || typeof(IServiceProvider).IsAssignableFrom(parameter.ParameterType))
                    {
                        Error("CB0001", constructor, $"receives {parameter.ParameterType.Name}: use [On] to connect domains or call both managers from the consumer; do not inject managers or service providers.");
                    }
                }
            }
        }

        void Inputs(Type manager)
        {
            foreach (var method in Methods(manager))
            {
                foreach (var parameter in method.GetParameters().Where(parameter => !Simple(parameter.ParameterType)))
                {
                    Error("CB0002", method, $"parameter {parameter.Name} uses {parameter.ParameterType.Name}: accept only simple values, such as identifiers, dates or strings.");
                }
            }
        }

        void Persistence(Type persistence)
        {
            var count = managers.SelectMany(manager => manager.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                .Count(constructor => constructor.GetParameters().Any(parameter => parameter.ParameterType == persistence));
            if (count != 1)
            {
                Error("CB0003", persistence, $"is received by {count} manager constructors: inject persistence into exactly one manager constructor.");
            }
        }

        void Publisher(Type publisher)
        {
            foreach (var method in Methods(publisher))
            {
                if (method.IsGenericMethod || method.GetParameters().Any(parameter => AnyEvent(parameter.ParameterType)))
                {
                    Error("CB0004", method, "accepts any event or is generic: expose a non-generic method with a specific event payload.");
                }
            }
            foreach (var item in publisher.GetEvents(Public))
            {
                if (item.EventHandlerType!.GetMethod("Invoke")!.GetParameters().Any(parameter => AnyEvent(parameter.ParameterType)))
                {
                    Error("CB0004", item, "accepts any event: declare an event with a specific payload.");
                }
                Summary(item);
            }
        }

        void Immutable(Type model)
        {
            foreach (var property in model.GetProperties(Public))
            {
                if (property.SetMethod is { IsPublic: true } setter
                    && !setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)))
                {
                    Error("CB0006", property, "has a public setter: use init or a read-only property.");
                }
            }
            foreach (var field in model.GetFields(Public).Where(field => !field.IsInitOnly && !field.IsLiteral))
            {
                Error("CB0006", field, "is a mutable public field: make it readonly or use an init property.");
            }
        }

        void Summary(MemberInfo member)
        {
            if (comments.Summary(member).Length == 0)
            {
                Error("CB0005", member, "has no summary: add an XML <summary> so its document can be generated.");
            }
        }

        void Error(string code, MemberInfo member, string message)
        {
            var type = member as Type ?? member.DeclaringType!;
            var source = SourceFiles.Find(member) ?? type.Assembly.Location;
            var name = member is Type ? type.FullName : type.FullName + "." + member.Name;
            errors.Add($"{source}: error {code}: {name}: {message}");
        }
    }

    private static bool Role<T>(Type type) where T : Attribute => type.IsDefined(typeof(T), false);

    private static IEnumerable<MethodInfo> Methods(Type type) => type.GetMethods(Public)
        .Where(method => !method.IsSpecialName && method.DeclaringType != typeof(object)
            && !method.IsDefined(typeof(CompilerGeneratedAttribute), false));

    private static bool Simple(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive || type.IsEnum || type == typeof(decimal) || type == typeof(string)
            || type == typeof(Guid) || type == typeof(DateTime) || type == typeof(DateTimeOffset)
            || type == typeof(DateOnly) || type == typeof(TimeOnly) || type == typeof(TimeSpan)
            || type == typeof(CancellationToken);
    }

    private static bool AnyEvent(Type type) => type == typeof(object) || type == typeof(EventArgs)
        || type.IsGenericParameter || (type.HasElementType && AnyEvent(type.GetElementType()!))
        || (type.IsGenericType && type.GetGenericArguments().Any(AnyEvent));
}
