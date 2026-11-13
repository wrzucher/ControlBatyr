using ControlBatyr.Inspection;
using Xunit;

namespace ControlBatyr.Tests;

public sealed class ProductNoiseTests
{
    private static BusinessApiModel Read() => BusinessApiReader.Read(null, typeof(ProductNoiseTests).Assembly);

    [Fact]
    public void Class_models_exclude_object_overrides_and_equatable_implementations()
    {
        var model = Read().Models.Single(item => item.Node.Name == nameof(EquatableTicket));
        Assert.Equal(["Approve"], model.Methods.Select(item => item.Node.Name));
    }

    [Fact]
    public void Record_models_include_only_authored_subject_methods()
    {
        var model = Read();
        Assert.Empty(model.Models.Single(item => item.Node.Name == nameof(TicketRecord)).Methods);
        Assert.Empty(model.Models.Single(item => item.Node.Name == nameof(TicketValue)).Methods);
        Assert.Equal(["Deconstruct"],
            model.Models.Single(item => item.Node.Name == nameof(AuthoredTicket)).Methods.Select(item => item.Node.Name));
    }

    [Fact]
    public void Tuple_properties_parameters_and_async_returns_keep_element_names_and_nullability()
    {
        var documents = BusinessApiMarkdown.Render(Read()).ToDictionary(item => item.RelativePath, item => item.Markdown);
        var text = documents["ticketnoise.reference.md"];
        Assert.Contains("| Selection | (string? ModelKey, string? Effort) |", text);
        Assert.Contains("| selection | (string? ModelKey, string? Effort) |", text);
        Assert.Contains("**Returns:** (string? ModelKey, string? Effort)", text);
        Assert.Equal("| Nested | List&lt;((string? Key, int Count) Item, string? Label)?&gt; |",
            text.Split('\n').Single(line => line.StartsWith("| Nested |", StringComparison.Ordinal)));
        Assert.Contains("| Long | (int A, int B, int C, int D, int E, int F, int G, string? H, int I) |", text);
        Assert.Contains("| Unnamed | (string?, int)[] |", text);
        Assert.Contains("| NestedLong | ((int A, int B, int C, int D, int E, int F, int G, string? H, int I) Items, (string? Key, int Count) Tail) |", text);
        Assert.DoesNotContain("ValueTuple", text);
        var model = Read();
        var restored = System.Text.Json.JsonSerializer.Deserialize<BusinessApiModel>(System.Text.Json.JsonSerializer.Serialize(model))!;
        Assert.Equal(documents["ticketnoise.reference.md"], BusinessApiMarkdown.Render(restored).Single(item => item.RelativePath == "ticketnoise.reference.md").Markdown);
    }

    [Fact]
    public void Index_links_to_shared_models_exactly_when_the_document_exists()
    {
        var full = Read();
        var model = full with
        {
            Domains = [.. full.Domains.Where(item => item.Name is "TicketNoise" or "OtherTicketNoise")],
            Models = [.. full.Models.Where(item => item.Node.Name is nameof(TicketRecord) or nameof(TicketValue) or nameof(AuthoredTicket) or nameof(EquatableTicket) or nameof(TupleTicket))],
            Connections = []
        };
        Check(model);
        Check(model with { Domains = [model.Domains.Single(item => item.Name == "TicketNoise")] });

        static void Check(BusinessApiModel model)
        {
            var documents = BusinessApiMarkdown.Render(model).ToDictionary(item => item.RelativePath, item => item.Markdown);
            Assert.Equal(documents.ContainsKey("shared.md"), documents["index.md"].Contains("[Shared models](shared.md)", StringComparison.Ordinal));
        }
    }
}

[Manager]
internal sealed class TicketNoiseManager
{
    public TicketRecord Read() => new(1);
    public TicketValue ReadValue() => new(1);
    public AuthoredTicket ReadAuthored() => new();
    public EquatableTicket ReadEquatable() => new();
    public TupleTicket ReadTuple() => new();
    public Task<(string? ModelKey, string? Effort)> SelectAsync((string? ModelKey, string? Effort) selection) => Task.FromResult(selection);
}

[Manager]
internal sealed class OtherTicketNoiseManager
{
    public TicketRecord Read() => new(2);
}

internal sealed record TicketRecord(int Id);
internal readonly record struct TicketValue(int Id);

internal sealed class EquatableTicket : IEquatable<EquatableTicket>
{
    public override bool Equals(object? obj) => obj is EquatableTicket other && Equals(other);
    public override int GetHashCode() => 0;
    public override string ToString() => "Ticket";
    public bool Equals(EquatableTicket? other) => other is not null;
    public void Approve() { }
}

internal sealed record AuthoredTicket
{
    public override string ToString() => "Ticket";
    public override int GetHashCode() => 0;
    public bool Equals(AuthoredTicket? other) => other is not null;
    public void Deconstruct(out int id) => id = 0;
}

internal sealed class TupleTicket
{
    public (string? ModelKey, string? Effort) Selection { get; init; }
    public List<((string? Key, int Count) Item, string? Label)?> Nested { get; init; } = [];
    public (int A, int B, int C, int D, int E, int F, int G, string? H, int I) Long { get; init; }
    public (string?, int)[] Unnamed { get; init; } = [];
    public ((int A, int B, int C, int D, int E, int F, int G, string? H, int I) Items, (string? Key, int Count) Tail) NestedLong { get; init; }
}
