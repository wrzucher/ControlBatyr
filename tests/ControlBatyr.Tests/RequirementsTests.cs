using System.Runtime.CompilerServices;
using ControlBatyr.Inspection;
using ControlBatyr.Requirements;
using Xunit;

namespace ControlBatyr.Tests;

public sealed class RequirementsTests
{
    private static Dictionary<string, string> Documents() => BusinessApi.Generate(
        Path.Combine(Path.GetDirectoryName(TestFile())!, "docs"), typeof(RequirementsTests).Assembly)
        .ToDictionary(document => document.RelativePath, document => document.Markdown);

    [Fact]
    [Requirement<Gateway>(nameof(Gateway.FindAsync))]
    public void requirements_keep_their_initial_case_and_link_only_to_the_selected_operation()
    {
        var markdown = Documents()["gateway.md"];
        var find = markdown[markdown.IndexOf("#### Find\n", StringComparison.Ordinal)..markdown.IndexOf("#### Save\n", StringComparison.Ordinal)];
        Assert.Contains("- requirements keep their initial case and link only to the selected operation — [Source](../RequirementsTests.cs)", find);
        Assert.DoesNotContain("- requirements keep", markdown[markdown.IndexOf("#### Save\n", StringComparison.Ordinal)..]);
        Assert.DoesNotContain("- Unmarked tests", markdown);
    }

    /// <summary>Summaries replace test names.</summary>
    [Fact]
    [Requirement<Gateway>(nameof(Gateway.SaveAsync))]
    public void A_summary_overrides_the_method_name()
    {
        var markdown = Documents()["gateway.md"];
        Assert.Contains("- Summaries replace test names. — [Source](../RequirementsTests.cs)", markdown);
        Assert.DoesNotContain("- A summary overrides", markdown);
        Assert.Equal(2, markdown.Split("- Summaries replace test names.", StringSplitOptions.None).Length - 1);
    }

    /// <summary>Summaries replace test names.</summary>
    [Fact]
    [Requirement<Gateway>(nameof(Gateway.SaveAsync))]
    public void Duplicate_text_keeps_both_tests_under_the_operation()
    {
        var markdown = Documents()["gateway.md"];
        var save = markdown[markdown.IndexOf("#### Save\n", StringComparison.Ordinal)..markdown.IndexOf("#### All\n", StringComparison.Ordinal)];
        Assert.Equal(2, save.Split("- Summaries replace test names.", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("- Summaries replace test names.", markdown[..markdown.IndexOf("#### Save\n", StringComparison.Ordinal)]);
    }

    [Fact]
    public void Unmarked_tests_are_omitted_and_uncovered_operations_remain_visible()
    {
        var documents = Documents();
        var markdown = documents["gateway.md"];
        var all = markdown[markdown.IndexOf("#### All\n", StringComparison.Ordinal)..markdown.IndexOf("#### Echo\n", StringComparison.Ordinal)];
        Assert.DoesNotContain("**Requirements**", all);
        Assert.DoesNotContain("No requirements", markdown);
        Assert.DoesNotContain("- Unmarked tests are omitted", markdown);
        Assert.DoesNotContain("operations have requirements", documents["index.md"]);
    }

    [Fact]
    public void Invalid_targets_report_CB0007_at_the_test_source()
    {
        var errors = Rules.Check(typeof(InvalidRequirements));
        Assert.Equal(5, errors.Count);
        Assert.All(errors, error => Assert.StartsWith(TestFile() + ": error CB0007:", error));
        foreach (var name in new[] { "Missing", "Private", "Unmarked", "Property", "ObjectMethod" })
        {
            Assert.Contains(errors, error => error.Contains("InvalidRequirements." + name + ":", StringComparison.Ordinal));
        }
        Assert.Empty(Rules.Check(typeof(RequirementsTests)));
    }

    [Fact]
    public void Invalid_targets_are_not_attached_to_any_documented_operation()
    {
        var markdown = string.Join("\n", Documents().Values);
        foreach (var name in new[] { "Missing", "Private", "Unmarked", "Property", "ObjectMethod" })
        {
            Assert.DoesNotContain("- " + name + " —", markdown);
        }
    }

    private static string TestFile([CallerFilePath] string path = "") => path;

    private sealed class InvalidRequirements
    {
        [Requirement<Gateway>("Missing")]
        public void Missing() { }
        [Requirement<Target>("Hidden")]
        public void Private() { }
        [Requirement<UnmarkedManager>(nameof(UnmarkedManager.HiddenOperation))]
        public void Unmarked() { }
        [Requirement<Target>("get_Value")]
        public void Property() { }
        [Requirement<Gateway>(nameof(ToString))]
        public void ObjectMethod() { }
    }

    [Manager]
    public sealed class Target
    {
        private void Hidden() { }
        public int Value => 1;
    }
}
