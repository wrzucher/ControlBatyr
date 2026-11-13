namespace ControlBatyr.Inspection;

/// <summary>Renders business API Markdown without loading product assemblies.</summary>
public static class BusinessApiMarkdown
{
    /// <summary>Returns an index, an overview and reference per domain, and shared models when needed.</summary>
    public static IReadOnlyList<(string RelativePath, string Markdown)> Render(BusinessApiModel model)
    {
        return new MarkdownRenderer(model).Render();
    }
}
