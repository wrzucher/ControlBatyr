using System.Reflection;

namespace ControlBatyr.Inspection;

internal static class ProductTypes
{
    internal static Type[] Find(params Assembly[] assemblies)
        => Find(Assembly.Load, assemblies);

    internal static Type[] Find(Func<AssemblyName, Assembly> load, params Assembly[] assemblies)
    {
        var found = new HashSet<Assembly>(assemblies);
        var pending = new Queue<Assembly>(assemblies);
        while (pending.TryDequeue(out var assembly))
        {
            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                var dependency = load(reference);
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
}
