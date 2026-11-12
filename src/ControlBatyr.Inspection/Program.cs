using System.Reflection;
using System.Runtime.Loader;
using ControlBatyr.Inspection;

try
{
    if (args.Length != 3)
    {
        throw new ArgumentException("Usage: ControlBatyr.Inspection <documentation folder> <assembly list file> <reference list file>");
    }

    var assemblies = File.ReadAllLines(args[1]).Where(line => !string.IsNullOrWhiteSpace(line)).Select(Path.GetFullPath).ToArray();
    if (assemblies.Length == 0)
    {
        throw new ArgumentException("At least one business API assembly is required.");
    }
    var references = File.ReadAllLines(args[2]).Concat(assemblies)
        .Where(path => Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase))
        .Select(Path.GetFullPath).Distinct().ToArray();
    AssemblyLoadContext.Default.Resolving += (_, name) =>
    {
        var path = references.FirstOrDefault(path => Path.GetFileNameWithoutExtension(path).Equals(name.Name, StringComparison.OrdinalIgnoreCase));
        return path is null ? null : AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
    };
    var directory = Path.GetFullPath(args[0]);
    var cores = assemblies.Select(AssemblyLoadContext.Default.LoadFromAssemblyPath).ToArray();
    var violations = Rules.Check(Rules.ProductTypes(cores));
    foreach (var violation in violations)
    {
        Console.Error.WriteLine(violation);
    }
    if (violations.Count > 0)
    {
        return 1;
    }
    var documents = BusinessApi.Generate(directory, cores);
    GeneratedFiles.Write(directory, documents);
    Console.WriteLine($"ControlBatyr: generated {documents.Count} business API documents in {directory}.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"ControlBatyr business API generation failed: {exception.Message}");
    if (exception is ReflectionTypeLoadException loadException)
    {
        foreach (var failure in loadException.LoaderExceptions)
        {
            Console.Error.WriteLine(failure?.Message);
        }
    }
    return 1;
}
