using System.Reflection;

namespace ControlBatyr.Inspection;

/// <summary>Generates business documentation from compiled assemblies and adjacent XML documentation.</summary>
public static class BusinessApi
{
    /// <summary>Returns an index, one document per manager, and shared models when needed.</summary>
    public static IReadOnlyList<(string RelativePath, string Markdown)> Generate(params Assembly[] assemblies) => new BusinessDocument(assemblies, null).Render();

    /// <summary>Also includes source links relative to the document, using adjacent portable PDB files.</summary>
    /// <param name="documentDirectory">The intended document directory, used only for source links; generation does not write files.</param>
    /// <param name="assemblies">The compiled core assemblies.</param>
    public static IReadOnlyList<(string RelativePath, string Markdown)> Generate(string documentDirectory, params Assembly[] assemblies) => new BusinessDocument(assemblies, documentDirectory).Render();
}
