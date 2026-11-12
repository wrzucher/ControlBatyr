using System.Reflection;
using ControlBatyr.Inspection;

namespace ControlBatyr.Tests;

internal static class BusinessApi
{
    internal static IReadOnlyList<(string RelativePath, string Markdown)> Generate(params Assembly[] assemblies)
    {
        return Generate(null, assemblies);
    }

    internal static IReadOnlyList<(string RelativePath, string Markdown)> Generate(string? directory, params Assembly[] assemblies)
    {
        return BusinessApiMarkdown.Render(BusinessApiReader.Read(directory, assemblies));
    }
}
