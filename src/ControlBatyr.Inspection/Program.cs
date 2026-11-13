using System.Reflection;
using System.Runtime.Loader;
using ControlBatyr.Inspection;

try
{
    if (args.Length != 6)
    {
        throw new ArgumentException("Usage: ControlBatyr.Inspection <documentation folder> <assembly list file> <reference list file> <rules assembly or empty> <check documents> <outputs>");
    }

    var outputs = Outputs.Select(args[5]);
    var assemblies = File.ReadAllLines(args[1]).Where(line => !string.IsNullOrWhiteSpace(line)).Select(Path.GetFullPath).ToArray();
    if (assemblies.Length == 0)
    {
        throw new ArgumentException("At least one business API assembly is required.");
    }
    var references = File.ReadAllLines(args[2]).Concat(assemblies)
        .Where(path => Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase))
        .Select(Path.GetFullPath).Distinct().ToArray();
    using var context = new MetadataLoadContext(new PathAssemblyResolver(references), "System.Runtime");
    var directory = Path.GetFullPath(args[0]);
    var cores = assemblies.Select(context.LoadFromAssemblyPath).ToArray();
    var types = ProductTypes.Find(context.LoadFromAssemblyName, cores);
    var violations = string.IsNullOrEmpty(args[3]) ? [] :
        (IReadOnlyList<string>)AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[3]))
            .GetType("ControlBatyr.Inspection.Rules", throwOnError: true)!
            .GetMethod("Check", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [types])!;
    foreach (var violation in violations)
    {
        Console.Error.WriteLine(violation);
    }
    if (violations.Count > 0)
    {
        return 1;
    }
    var model = BusinessApiReader.Read(directory, types.Select(type => type.Assembly).Distinct().ToArray());
    var documents = outputs.SelectMany(output => output.Render(model)).ToArray();
    bool IsEnabled(string file) => outputs.Any(output => output.Owns(file));
    if (args[4].Equals("true", StringComparison.OrdinalIgnoreCase))
    {
        if (outputs.Any(output => output.Name != "mermaid"))
        {
            GeneratedFiles.Check(directory, documents, IsEnabled);
        }
    }
    else
    {
        if (outputs.Any(output => output.Name != "mermaid"))
        {
            GeneratedFiles.Write(directory, documents, IsEnabled);
        }
    }
    if (outputs.Any(output => output.Name == "mermaid") && !outputs.Any(output => output.Name == "markdown"))
    {
        Console.WriteLine("ControlBatyr: mermaid requires markdown; no diagrams written because they live inside Markdown pages.");
    }
    Console.WriteLine($"ControlBatyr: {(args[4].Equals("true", StringComparison.OrdinalIgnoreCase) ? "checked" : "generated")} {documents.Length} business API documents in {directory}.");
    return 0;
}
catch (Exception exception)
{
    var failure = exception is TargetInvocationException { InnerException: { } inner } ? inner : exception;
    Console.Error.WriteLine($"ControlBatyr business API generation failed: {failure.Message}");
    if (exception is ReflectionTypeLoadException loadException)
    {
        foreach (var loaderFailure in loadException.LoaderExceptions)
        {
            Console.Error.WriteLine(loaderFailure?.Message);
        }
    }
    return 1;
}
