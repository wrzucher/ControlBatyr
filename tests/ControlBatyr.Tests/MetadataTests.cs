using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using ControlBatyr.Inspection;
using Xunit;

namespace ControlBatyr.Tests;

public sealed class MetadataTests
{
    [Fact]
    public void Metadata_preserves_documents_nullability_and_every_rule_diagnostic()
    {
        var assembly = typeof(MetadataTests).Assembly;
        var paths = Directory.GetFiles(AppContext.BaseDirectory, "*.dll")
            .Concat(Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll"));
        using var context = new MetadataLoadContext(new PathAssemblyResolver(paths), "System.Runtime");
        var metadata = context.LoadFromAssemblyPath(assembly.Location);

        Assert.Equal(JsonSerializer.Serialize(BusinessApiReader.Read(null, assembly)),
            JsonSerializer.Serialize(BusinessApiReader.Read(null, metadata)));
        var reference = BusinessApiMarkdown.Render(BusinessApiReader.Read(null, metadata))
            .Single(document => document.RelativePath == "ticketnoise.reference.md").Markdown;
        Assert.Contains("| Nested | List&lt;((string? Key, int Count) Item, string? Label)?&gt; |", reference);
        var expected = Rules.Check(assembly.GetTypes());
        Assert.All(Enumerable.Range(1, 7), code => Assert.Contains(expected, error => error.Contains($"error CB{code:0000}:")));
        Assert.Equal(expected, Rules.Check(metadata.GetTypes()));
    }
}
