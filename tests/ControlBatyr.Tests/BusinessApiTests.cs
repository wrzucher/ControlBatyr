using ControlBatyr.Inspection;
using Xunit;

namespace ControlBatyr.Tests;

public sealed class BusinessApiTests
{
    private static string Generate() => BusinessApi.Generate(typeof(BusinessApiTests).Assembly);

    [Fact]
    public void Attributes_select_roles_and_exclude_unmarked_and_persistence_types()
    {
        var markdown = Generate();

        Assert.Contains("## Managers\n", markdown);
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
    public void Business_signatures_hide_async_plumbing_and_link_nullable_results()
    {
        var markdown = Generate();

        Assert.Contains("Find(id: Guid) → [Snapshot](#type-controlbatyr-tests-snapshot)?", markdown);
        Assert.Contains("Save(id: Guid) → void", markdown);
        Assert.Contains("All() → IReadOnlyList&lt;[Snapshot](#type-controlbatyr-tests-snapshot)&gt;", markdown);
        Assert.DoesNotContain("CancellationToken", markdown);
        Assert.DoesNotContain("Task", markdown);
        Assert.DoesNotContain("FindAsync", markdown);
    }

    [Fact]
    public void Comments_describe_data_computed_properties_methods_events_and_enum_values()
    {
        var markdown = Generate();

        Assert.Contains("A stored reading.", markdown);
        Assert.Contains("| State | ReadingState | Whether the reading is usable. |", markdown);
        var data = markdown[markdown.IndexOf("#### Data", StringComparison.Ordinal)..markdown.IndexOf("#### Computed properties", StringComparison.Ordinal)];
        Assert.Contains("| SourceId | Guid | Identifies the source of the reading. |", data);
        Assert.Contains("#### Computed properties", markdown);
        Assert.Contains("| IsReady | bool | Whether the reading can be consumed. |", markdown);
        Assert.Contains("Label(prefix: string) → string | Gives the reading a display label.", markdown);
        Assert.Contains("| Recorded | [Snapshot](#type-controlbatyr-tests-snapshot) | A reading became available. |", markdown);
        Assert.Contains("#### ReadingState", markdown);
        Assert.Contains("| Ready = 1 | Available for use. |", markdown);
        Assert.Contains("Find(id: Guid)", markdown);
        Assert.Contains("Looks up a reading.", markdown);
        Assert.Contains("Echo(values: IReadOnlyList&lt;string&gt;) → string | Joins supplied labels.", markdown);
    }

    [Fact]
    public void Missing_comments_are_placeholders_instead_of_failures()
    {
        var markdown = Generate();

        Assert.Contains("### Gateway\n\n—", markdown);
        Assert.Contains("Save(id: Guid) → void | — |", markdown);
        Assert.Contains("| Sequence | int | — |", markdown);
        Assert.Contains("| Unavailable = 0 | — |", markdown);
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
}

[Manager]
public sealed class Gateway
{
    /// <summary>Looks up a reading.</summary>
    public Task<Snapshot?> FindAsync(Guid id, CancellationToken ct) => Task.FromResult<Snapshot?>(null);
    public ValueTask SaveAsync(Guid id, CancellationToken ct) => ValueTask.CompletedTask;
    public IReadOnlyList<Snapshot> All() => [];
    /// <summary>Joins supplied labels.</summary>
    public string Echo(IReadOnlyList<string> values) => string.Join(", ", values);
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
    /// <summary>Gives the reading a display label.</summary>
    public string Label(string prefix) => prefix + Sequence;
}

public enum ReadingState
{
    Unavailable,
    /// <summary>Available for use.</summary>
    Ready
}

[EventPublisher]
public sealed class Feed
{
    /// <summary>A reading became available.</summary>
    public event Action<Snapshot>? Recorded;
    internal void NotifyRecorded(Snapshot snapshot) => Recorded?.Invoke(snapshot);
    public void PublicHelper() { }
}

public sealed class UnmarkedManager
{
    public void HiddenOperation() { }
}

[Persistence, Manager]
public sealed class HiddenStore;
