using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ControlBatyr.Inspection;

internal sealed record Output(string Name, Func<string, bool> Owns,
    Func<BusinessApiModel, IEnumerable<(string RelativePath, string Content)>> Render);

internal static class Outputs
{
    private static readonly Output[] Known =
    [
        new("markdown", file => file.EndsWith(".md", StringComparison.Ordinal), BusinessApiMarkdown.Render),
        new("model", file => file == "business-api.json", model => [("business-api.json",
            JsonSerializer.Serialize(model, new JsonSerializerOptions
            {
                WriteIndented = true, NewLine = "\n", PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            }) + "\n")]),
        new("mermaid", _ => false, _ => [])
    ];

    internal static bool IsKnownFile(string file) => Known.Any(output => output.Owns(file));

    internal static Output[] Select(string names)
    {
        var requested = names.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknown = requested.Where(name => !Known.Any(output => output.Name.Equals(name, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (unknown.Length > 0)
        {
            throw new ArgumentException($"Unknown ControlBatyrOutputs name(s): {string.Join(", ", unknown)}. Known names: {string.Join(", ", Known.Select(output => output.Name))}.");
        }
        return Known.Where(output => requested.Contains(output.Name)).Select(output =>
            output.Name == "markdown" && requested.Contains("mermaid")
                ? output with { Render = model => new MarkdownRenderer(model, diagrams: true).Render() } : output).ToArray();
    }
}
