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
    public async Task Unknown_names_fail_clearly_before_writing()
    {
        var result = await Run("markdown;unknown");

        Assert.NotEqual(0, result.Code);
        Assert.Contains("Unknown ControlBatyrOutputs name(s): unknown. Known names: markdown, model.", result.Error);
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

    private async Task<(int Code, string Error)> Run(string outputs, bool check = false)
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
            await output;
            return (process.ExitCode, await error);
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
