using System.Reflection;

namespace ControlBatyr.Inspection;

/// <summary>Generates business documentation from compiled assemblies and adjacent XML documentation.</summary>
public static class BusinessApi
{
    /// <summary>Returns stable Markdown for the attributed API and its reachable models.</summary>
    public static string Generate(params Assembly[] assemblies) => new BusinessDocument(assemblies, null).Render();

    /// <summary>Also includes source links relative to the document, using adjacent portable PDB files.</summary>
    /// <param name="documentPath">The intended Markdown file path; generation does not write the file.</param>
    /// <param name="assemblies">The compiled core assemblies.</param>
    public static string Generate(string documentPath, params Assembly[] assemblies) => new BusinessDocument(assemblies, documentPath).Render();
}
