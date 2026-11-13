using System.Diagnostics;
using ControlBatyr.Inspection;
using Xunit;

namespace ControlBatyr.Tests;

public sealed class OutputsTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "controlbatyr-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("MoDeL", "business-api.json")]
    [InlineData("MARKDOWN", "index.md")]
    public async Task The_executable_writes_only_selected_outputs(string outputs, string expected)
    {
        var result = await Run(outputs);

        Assert.Equal(0, result.Code);
        var files = Directory.GetFiles(directory).Select(Path.GetFileName).ToArray();
        Assert.Contains(expected, files);
        if (outputs.Equals("model", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Equal(new[] { ".controlbatyr-generated", "business-api.json" }, files.Order());
        }
        else
        {
            Assert.DoesNotContain("business-api.json", files);
            Assert.All(files.Where(file => file != ".controlbatyr-generated"), file => Assert.EndsWith(".md", file));
        }
    }

    [Fact]
    public async Task Mermaid_shows_operations_models_events_and_identifiers_in_domain_overviews()
    {
        Assert.Equal(0, (await Run("markdown;mermaid")).Code);
        var diagram = File.ReadAllText(Path.Combine(directory, "game.md"));
        var model = BusinessApiReader.Read(null, ProductTypes.Find(typeof(Game.Tests.GameRequirements).Assembly)
            .Select(type => type.Assembly).Distinct().ToArray());
        var domain = model.Domains.Single(item => item.Name == "Game");

        Assert.Contains("## Diagram\n\n```mermaid\nclassDiagram\n%% @arch/1 diagram profile=class-v1\n", diagram);
        foreach (var operation in domain.Operations)
        {
            Assert.Contains("+" + operation.Node.Name + "()", diagram);
        }
        Assert.Contains("<<event>>", diagram);
        Assert.Contains(" : raises PlaceBet", diagram);
        Assert.Contains(" : on\n", diagram);
        Assert.Contains("<<record>>", diagram);
        Assert.Contains("<<enumeration>>", diagram);
        Assert.DoesNotContain("[\"BetState\"]", diagram);
        Assert.DoesNotContain("[\"BetCancelledEventArgs\"]", diagram);
        Assert.DoesNotContain("[\"BetPlacedEventArgs\"]", diagram);
        Assert.All(diagram.Split('\n').Where(line => line.StartsWith("    +", StringComparison.Ordinal)),
            line => Assert.EndsWith("()", line));
        Assert.Contains("%% @arch/1 type id=\"" + domain.Node.Id + "\"\nclass ", diagram);
        var recordDiagram = File.ReadAllText(Path.Combine(directory, "account.md"));
        Assert.Contains("[\"BetRefunded\"] {\n    <<event>>\n}", recordDiagram);
        Assert.All(recordDiagram.Split('\n').Where(line => line.StartsWith("    +", StringComparison.Ordinal)),
            line => Assert.EndsWith("()", line));
        Assert.Contains(" : on\n", recordDiagram);
        Assert.Contains("%% @arch/1 type id=\"A:Game.Core|T:Game.Core.BetRefunded\"\nclass ", recordDiagram);
        Assert.DoesNotContain("## Diagram", File.ReadAllText(Path.Combine(directory, "game.reference.md")));
    }

    [Fact]
    public void Mermaid_includes_an_event_raised_from_another_domain()
    {
        var model = BusinessApiReader.Read(null, ProductTypes.Find(typeof(Game.Tests.GameRequirements).Assembly)
            .Select(type => type.Assembly).Distinct().ToArray());
        var domain = model.Domains.Single(item => item.Name == "Game");
        var refunded = model.Domains.Single(item => item.Name == "Account").RecordEvents.Single();
        var operation = domain.Operations[0] with { Raises = [refunded.Node.Id] };
        domain = domain with { Operations = [operation] };

        var diagram = MermaidRenderer.Render(model, domain);

        Assert.Contains("[\"BetRefunded\"] {\n    <<event>>", diagram);
        Assert.Contains("%% @arch/1 type id=\"" + refunded.Node.Id + "\"\nclass ", diagram);
        Assert.Contains(" : raises CreateGame\n", diagram);
    }

    [Fact]
    public void Mermaid_unwraps_signature_types_and_uses_safe_class_keys_without_model_members()
    {
        var model = BusinessApiReader.Read(null, ProductTypes.Find(typeof(Game.Tests.GameRequirements).Assembly)
            .Select(type => type.Assembly).Distinct().ToArray());
        var domain = model.Domains.Single(item => item.Name == "Game");
        var bet = model.Models.Single(item => item.Node.Name == "Bet");
        var state = model.Models.Single(item => item.Node.Name == "BetState");
        var changed = bet with { Node = bet.Node with { Name = "class" } };
        model = model with { Models = model.Models.Replace(bet, changed) };
        var operation = domain.Operations[0] with
        {
            Parameters = [new("bets", new("", null, [new("Bet", bet.Node.Id, [], "?")], "[]"), "")],
            ReturnType = new("Task", null, [new("IReadOnlyList", null,
                [new("", null, [new("BetState", state.Node.Id, [], "")], "?")], "")], "")
        };
        domain = domain with { Operations = [operation], Publishers = [], RecordEvents = [] };
        model = model with { Connections = [] };

        var diagram = MermaidRenderer.Render(model, domain);

        Assert.Contains("class C1[\"class\"] {\n    <<record>>\n}", diagram);
        Assert.Contains("class C2[\"BetState\"] {\n    <<enumeration>>\n}", diagram);
        Assert.DoesNotContain("class class", diagram);
        Assert.Contains("C0 ..> C1\n", diagram);
        Assert.Contains("C0 ..> C2\n", diagram);
        Assert.Equal(3, diagram.Split('\n').Count(line => line.StartsWith("class ", StringComparison.Ordinal)));
        Assert.All(diagram.Split('\n').Where(line => line.StartsWith("    +", StringComparison.Ordinal)),
            line => Assert.EndsWith("()", line));
        Assert.Contains("%% @arch/1 type id=\"" + bet.Node.Id + "\"\nclass ", diagram);
    }

    [Fact]
    public async Task Mermaid_is_off_unless_selected_and_can_be_switched_off_again()
    {
        Assert.Equal(0, (await Run("markdown;model")).Code);
        Assert.DoesNotContain("## Diagram", File.ReadAllText(Path.Combine(directory, "game.md")));
        Assert.Equal(0, (await Run("markdown;model;mermaid")).Code);
        Assert.NotEqual(0, (await Run("markdown;model", check: true)).Code);
        Assert.Equal(0, (await Run("markdown;model")).Code);
        Assert.DoesNotContain("## Diagram", File.ReadAllText(Path.Combine(directory, "game.md")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Mermaid_without_markdown_writes_and_checks_nothing_and_explains_why(bool check)
    {
        var result = await Run("mermaid", check);

        Assert.Equal(0, result.Code);
        Assert.Contains("mermaid requires markdown", result.Output);
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task Unknown_names_fail_clearly_before_writing()
    {
        var result = await Run("markdown;unknown");

        Assert.NotEqual(0, result.Code);
        Assert.Contains("Unknown ControlBatyrOutputs name(s): unknown. Known names: markdown, model, mermaid.", result.Error);
        Assert.False(Directory.Exists(directory));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_empty_list_writes_and_checks_nothing(bool check)
    {
        var result = await Run(" ; ; ", check);

        Assert.Equal(0, result.Code);
        Assert.False(Directory.Exists(directory));
    }

    [Theory]
    [InlineData("model", "index.md", "obsolete.md")]
    [InlineData("markdown", "business-api.json", null)]
    public async Task Disabled_outputs_are_preserved_and_ignored_by_checks_and_can_be_enabled_again(
        string outputs, string disabledFile, string? obsoleteFile)
    {
        Assert.Equal(0, (await Run("markdown;model")).Code);
        File.WriteAllText(Path.Combine(directory, disabledFile), "Stale disabled output\n");
        if (obsoleteFile is not null)
        {
            File.WriteAllText(Path.Combine(directory, obsoleteFile), "Obsolete Markdown\n");
            File.AppendAllText(Path.Combine(directory, ".controlbatyr-generated"), obsoleteFile + "\n");
            var manifest = Path.Combine(directory, ".controlbatyr-generated");
            File.WriteAllLines(manifest, File.ReadAllLines(manifest).Order(StringComparer.Ordinal));
        }
        var before = Directory.GetFiles(directory).ToDictionary(path => path, File.ReadAllText);

        Assert.Equal(0, (await Run(outputs, check: true)).Code);
        Assert.Equal(0, (await Run(outputs)).Code);
        Assert.Equal(0, (await Run("", check: true)).Code);
        Assert.Equal(0, (await Run("")).Code);
        Assert.Equal(before.Keys.Order(), Directory.GetFiles(directory).Order());
        Assert.All(before, pair => Assert.Equal(pair.Value, File.ReadAllText(pair.Key)));
        Assert.NotEqual(0, (await Run("markdown;model", check: true)).Code);

        Assert.Equal(0, (await Run("markdown;model")).Code);
        Assert.Equal(0, (await Run("markdown;model", check: true)).Code);
        Assert.NotEqual("Stale disabled output\n", File.ReadAllText(Path.Combine(directory, disabledFile)));
        if (obsoleteFile is not null) Assert.False(File.Exists(Path.Combine(directory, obsoleteFile)));

        var enabledFile = outputs == "model" ? "business-api.json" : "index.md";
        File.WriteAllText(Path.Combine(directory, enabledFile), "Stale enabled output\n");
        var failure = await Run(outputs, check: true);
        Assert.NotEqual(0, failure.Code);
        Assert.Contains("documents are stale; rebuild and commit", failure.Error);
        Assert.Equal("Stale enabled output\n", File.ReadAllText(Path.Combine(directory, enabledFile)));
    }

    private async Task<(int Code, string Error, string Output)> Run(string outputs, bool check = false)
    {
        var lists = directory + "-lists";
        Directory.CreateDirectory(lists);
        try
        {
            var assemblyList = Path.Combine(lists, "assemblies.txt");
            var referenceList = Path.Combine(lists, "references.txt");
            File.WriteAllText(assemblyList, typeof(Game.Tests.GameRequirements).Assembly.Location);
            File.WriteAllLines(referenceList, Directory.GetFiles(AppContext.BaseDirectory, "*.dll"));
            var testAssembly = typeof(OutputsTests).Assembly.Location;
            var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(testAssembly, ".runtimeconfig.json"),
                "--depsfile", Path.ChangeExtension(testAssembly, ".deps.json"), typeof(BusinessApiReader).Assembly.Location,
                directory, assemblyList, referenceList, "", check ? "true" : "false", outputs })
            {
                start.ArgumentList.Add(argument);
            }
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            return (process.ExitCode, await error, await output);
        }
        finally
        {
            Directory.Delete(lists, recursive: true);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
