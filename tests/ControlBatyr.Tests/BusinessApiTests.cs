using System.Runtime.CompilerServices;
using ControlBatyr.Inspection;
using Xunit;

namespace ControlBatyr.Tests;

public sealed class BusinessApiTests
{
    private static string Generate() => BusinessApi.Generate(typeof(BusinessApiTests).Assembly).Single(document => document.RelativePath == "gateway.reference.md").Markdown;

    [Fact]
    public void Attributes_select_operations_and_events_but_unreachable_and_persistence_types_stay_hidden()
    {
        var markdown = Generate();

        Assert.Contains("## Operations\n", markdown);
        Assert.Contains("### Gateway\n", markdown);
        Assert.Contains("### Snapshot\n", markdown);
        Assert.Contains("### Feed\n", markdown);
        Assert.DoesNotContain(nameof(UnmarkedManager), markdown);
        Assert.DoesNotContain(nameof(HiddenStore), markdown);
        Assert.DoesNotContain(nameof(Feed.NotifyRecorded), markdown);
        Assert.DoesNotContain(nameof(Feed.PublicHelper), markdown);
        Assert.DoesNotContain(nameof(Snapshot.ToString), markdown);
        Assert.DoesNotContain("Deconstruct", markdown);
    }

    [Fact]
    public void Operations_show_parameters_returns_and_remarks_without_async_plumbing()
    {
        var markdown = Generate();
        var find = markdown[markdown.IndexOf("#### Find\n", StringComparison.Ordinal)..markdown.IndexOf("#### Save\n", StringComparison.Ordinal)];

        Assert.Contains("Looks up a reading.", find);
        Assert.Contains("| id | Guid | The reading to look up. |", find);
        Assert.Contains("**Returns:** [Snapshot](#type-controlbatyr-tests-snapshot)?\n\nThe reading, or null when id is unknown.", find);
        Assert.Contains("**Remarks**\n\nUses the current ControlBatyr.Tests.Snapshot.", find);
        Assert.Contains("IReadOnlyList&lt;[Snapshot](#type-controlbatyr-tests-snapshot)&gt;", markdown);
        Assert.DoesNotContain("CancellationToken", markdown);
        Assert.DoesNotContain("Task", markdown);
        Assert.DoesNotContain("FindAsync", markdown);
        Assert.DoesNotContain("Ignored example", markdown);
        Assert.DoesNotContain("Ignored exception", markdown);
    }

    [Fact]
    public void Reachable_models_are_defined_once_even_without_attributes_and_with_cycles()
    {
        var markdown = Generate();

        Assert.Equal(1, markdown.Split("## Models\n", StringSplitOptions.None).Length - 1);
        foreach (var name in new[] { "Snapshot", "ReadingState", "ReadingRecordedEventArgs", "ReadingDetails" })
        {
            Assert.Equal(1, markdown.Split($"### {name}\n", StringSplitOptions.None).Length - 1);
        }
        Assert.Contains("| Details | [ReadingDetails](#type-controlbatyr-tests-readingdetails)? |", markdown);
        Assert.Contains("| Parent | [Snapshot](#type-controlbatyr-tests-snapshot)? |", markdown);
        Assert.Contains("**Payload:** [ReadingRecordedEventArgs](#type-controlbatyr-tests-readingrecordedeventargs)", markdown);
        Assert.Contains("| Reading | [Snapshot](#type-controlbatyr-tests-snapshot) |", markdown);
        Assert.Contains("IReadOnlyList&lt;[ReadingState](#type-controlbatyr-tests-readingstate)&gt;", markdown);
    }

    [Fact]
    public void Primitive_and_framework_types_are_plain_text_and_have_no_model_blocks()
    {
        var markdown = Generate();

        Assert.DoesNotContain("type-system-", markdown);
        foreach (var name in new[] { "Guid", "string", "int", "bool", "decimal", "DateTimeOffset", "IReadOnlyList" })
        {
            Assert.DoesNotContain($"### {name}\n", markdown);
        }
        Assert.Contains("| id | Guid |", markdown);
        Assert.Contains("| SourceId | Guid |", markdown);
        Assert.Contains("| IsReady | bool |", markdown);
        Assert.Contains("| RecordedAt | DateTimeOffset |", markdown);
        Assert.Contains("| Value | decimal |", markdown);
        Assert.Contains("| values | IReadOnlyList&lt;string&gt; |", markdown);
        Assert.Contains("**Returns:** string\n", markdown);
    }

    [Fact]
    public void Model_comments_describe_data_computed_properties_methods_and_enum_values()
    {
        var markdown = Generate();

        Assert.Contains("A stored reading.", markdown);
        Assert.Contains("| State | [ReadingState](#type-controlbatyr-tests-readingstate) | Whether the reading is usable. |", markdown);
        var data = markdown[markdown.IndexOf("#### Data", StringComparison.Ordinal)..markdown.IndexOf("#### Computed properties", StringComparison.Ordinal)];
        Assert.Contains("| SourceId | Guid | Identifies the source of the reading. |", data);
        Assert.Contains("| IsReady | bool | Whether the reading can be consumed. |", markdown);
        Assert.Contains("#### Label\n\nGives the reading a display label.", markdown);
        Assert.Contains("| Ready = 1 | Available for use. |", markdown);
        Assert.Contains("#### Recorded\n\nA reading became available.", markdown);
        Assert.Contains("#### Echo\n\nJoins supplied labels.", markdown);
    }

    [Fact]
    public void Missing_comments_and_empty_operation_parts_are_omitted()
    {
        var markdown = Generate();
        var save = markdown[markdown.IndexOf("#### Save\n", StringComparison.Ordinal)..markdown.IndexOf("#### All\n", StringComparison.Ordinal)];

        Assert.DoesNotContain("—", markdown);
        Assert.DoesNotContain("Description", save);
        Assert.DoesNotContain("**Returns", save);
        Assert.DoesNotContain("**Remarks", save);
        Assert.Contains("| Sequence | int |  |", markdown);
        Assert.Contains("| Unavailable = 0 |  |", markdown);
    }

    [Fact]
    public void Declaration_order_is_preserved_and_enum_values_are_numeric()
    {
        var markdown = Generate();

        Assert.True(markdown.IndexOf("#### Find\n", StringComparison.Ordinal) < markdown.IndexOf("#### Save\n", StringComparison.Ordinal));
        Assert.True(markdown.IndexOf("#### Save\n", StringComparison.Ordinal) < markdown.IndexOf("#### All\n", StringComparison.Ordinal));
        Assert.True(markdown.IndexOf("| State |", StringComparison.Ordinal) < markdown.IndexOf("| Sequence |", StringComparison.Ordinal));
        Assert.True(markdown.IndexOf("### Snapshot\n", StringComparison.Ordinal) < markdown.IndexOf("### ReadingState\n", StringComparison.Ordinal));
        Assert.True(markdown.IndexOf("| Unavailable = 0 |", StringComparison.Ordinal) < markdown.IndexOf("| Ready = 1 |", StringComparison.Ordinal));
    }

    [Fact]
    public void Assembly_order_and_duplicates_do_not_change_the_document()
    {
        var core = typeof(BusinessApiTests).Assembly;
        var attributes = typeof(ManagerAttribute).Assembly;

        Assert.Equal(BusinessApi.Generate(core, attributes), BusinessApi.Generate(attributes, core, core));
        Assert.Equal(Generate(), Generate());
        Assert.DoesNotContain("\r", Generate());
    }

    [Fact]
    public void Source_links_use_the_document_directory_without_absolute_paths_or_line_numbers()
    {
        var directory = Path.GetDirectoryName(TestFile())!;
        var markdown = BusinessApi.Generate(Path.Combine(directory, "docs"), typeof(BusinessApiTests).Assembly).Single(document => document.RelativePath == "gateway.reference.md").Markdown;

        Assert.Contains("### Gateway\n\n[Source](../BusinessApiTests.cs)", markdown);
        Assert.Contains("### Snapshot\n\n[Source](../BusinessApiTests.cs)", markdown);
        Assert.Contains("### Feed\n\n[Source](../BusinessApiTests.cs)", markdown);
        Assert.DoesNotContain(directory, markdown);
        Assert.DoesNotContain(".cs:", markdown);
        Assert.DoesNotContain("[Source]", Generate());
    }

    private static string TestFile([CallerFilePath] string path = "") => path;
}

[Manager]
public sealed class Gateway(Feed feed)
{
    private readonly Feed feed = feed;
    /// <summary>Looks up a reading.</summary>
    /// <param name="id">The reading to look up.</param>
    /// <param name="ct"/>
    /// <returns>The reading, or null when <paramref name="id"/> is unknown.</returns>
    /// <remarks>Uses the current <see cref="Snapshot"/>.</remarks>
    /// <example>Ignored example</example>
    /// <exception cref="InvalidOperationException">Ignored exception</exception>
    public Task<Snapshot?> FindAsync(Guid id, CancellationToken ct) => Task.FromResult<Snapshot?>(null);
    public ValueTask SaveAsync(Guid id, CancellationToken ct)
    {
        feed.NotifyRecorded(new ReadingRecordedEventArgs(new Snapshot(ReadingState.Ready)));
        return ValueTask.CompletedTask;
    }
    public IReadOnlyList<Snapshot> All() => [];
    /// <summary>Joins supplied labels.</summary>
    public string Echo(IReadOnlyList<string> values) => string.Join(", ", values);
    public IReadOnlyList<ReadingState> States() => [];
}

/// <summary>A stored reading.</summary>
/// <param name="State">Whether the reading is usable.</param>
[ObjectModel]
public sealed record Snapshot(ReadingState State)
{
    public int Sequence { get; init; }
    /// <summary>Identifies the source of the reading.</summary>
    public Guid SourceId { get; } = Guid.Empty;
    /// <summary>Whether the reading can be consumed.</summary>
    public bool IsReady => State == ReadingState.Ready;
    public ReadingDetails? Details { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
    public decimal Value { get; init; }
    /// <summary>Gives the reading a display label.</summary>
    public string Label(string prefix) => prefix + Sequence;
}

public enum ReadingState
{
    /// <summary>Available for use.</summary>
    Ready = 1,
    Unavailable = 0
}

public sealed record ReadingRecordedEventArgs(Snapshot Reading);
public sealed record ReadingDetails(Snapshot? Parent);

[EventPublisher]
public sealed class Feed
{
    /// <summary>A reading became available.</summary>
    public event Action<ReadingRecordedEventArgs>? Recorded;
    internal void NotifyRecorded(ReadingRecordedEventArgs payload) => Recorded?.Invoke(payload);
    public void PublicHelper() { }
}

public sealed class UnmarkedManager
{
    public void HiddenOperation() { }
}

[Persistence, Manager]
public sealed class HiddenStore;
