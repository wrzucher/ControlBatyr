using System.Diagnostics;
using ControlBatyr.Inspection;
using Xunit;

namespace ControlBatyr.Tests;

public sealed class GeneratedFilesTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "controlbatyr-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Regeneration_replaces_owned_files_removes_obsolete_files_and_preserves_handwritten_files()
    {
        GeneratedFiles.Write(directory, [("index.md", "Old index\n"), ("obsolete.md", "Old domain\n")]);
        File.WriteAllText(Path.Combine(directory, "decisions.md"), "Handwritten decisions\n");

        GeneratedFiles.Write(directory, [("index.md", "Current index\n"), ("domain.md", "Current domain\n")]);

        Assert.Equal("Current index\n", File.ReadAllText(Path.Combine(directory, "index.md")));
        Assert.Equal("Current domain\n", File.ReadAllText(Path.Combine(directory, "domain.md")));
        Assert.False(File.Exists(Path.Combine(directory, "obsolete.md")));
        Assert.Equal("Handwritten decisions\n", File.ReadAllText(Path.Combine(directory, "decisions.md")));
        Assert.Equal("domain.md\nindex.md\n", File.ReadAllText(Path.Combine(directory, ".controlbatyr-generated")));
    }

    [Theory]
    [InlineData("domain.md")]
    [InlineData("business-api.json")]
    public void A_new_document_cannot_overwrite_a_handwritten_file_and_failure_preserves_the_old_set(string file)
    {
        GeneratedFiles.Write(directory, [("index.md", "Old index\n")]);
        File.WriteAllText(Path.Combine(directory, file), "Handwritten domain\n");

        var failure = Assert.Throws<InvalidOperationException>(() => GeneratedFiles.Write(directory,
            [("index.md", "New index\n"), (file, "Generated domain\n")]));

        Assert.Contains("unowned document: " + file, failure.Message);
        Assert.Equal("Handwritten domain\n", File.ReadAllText(Path.Combine(directory, file)));
        Assert.Equal("Old index\n", File.ReadAllText(Path.Combine(directory, "index.md")));
        Assert.Equal("index.md\n", File.ReadAllText(Path.Combine(directory, ".controlbatyr-generated")));
    }

    [Fact]
    public void Unchanged_documents_and_manifest_are_not_rewritten()
    {
        GeneratedFiles.Write(directory, [("index.md", "Index\n")]);
        var paths = Directory.GetFiles(directory);
        var timestamp = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        foreach (var path in paths)
        {
            File.SetLastWriteTimeUtc(path, timestamp);
        }

        GeneratedFiles.Write(directory, [("index.md", "Index\n")]);

        Assert.All(paths, path => Assert.Equal(timestamp, File.GetLastWriteTimeUtc(path)));
    }

    [Fact]
    public void Check_accepts_current_documents_and_preserves_every_file()
    {
        GeneratedFiles.Write(directory, [("index.md", "Index\n")]);
        File.WriteAllText(Path.Combine(directory, "decisions.md"), "Handwritten\n");
        var paths = Directory.GetFiles(directory);
        var timestamp = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        foreach (var path in paths) File.SetLastWriteTimeUtc(path, timestamp);

        GeneratedFiles.Check(directory, [("index.md", "Index\n")]);

        Assert.All(paths, path => Assert.Equal(timestamp, File.GetLastWriteTimeUtc(path)));
        Assert.Equal("Handwritten\n", File.ReadAllText(Path.Combine(directory, "decisions.md")));
    }

    [Theory]
    [InlineData("changed")]
    [InlineData("missing")]
    [InlineData("new")]
    [InlineData("obsolete")]
    [InlineData("manifest")]
    public void Check_rejects_stale_documents_or_manifest_without_writing(string change)
    {
        GeneratedFiles.Write(directory, [("index.md", "Index\n")]);
        if (change == "changed") File.WriteAllText(Path.Combine(directory, "index.md"), "Old\n");
        if (change == "missing") File.Delete(Path.Combine(directory, "index.md"));
        if (change == "manifest") File.Delete(Path.Combine(directory, ".controlbatyr-generated"));
        var before = Directory.GetFiles(directory).ToDictionary(path => path, File.ReadAllText);
        (string, string)[] expected = change switch
        {
            "new" => [("index.md", "Index\n"), ("new.md", "New\n")],
            "obsolete" => [],
            _ => [("index.md", "Index\n")]
        };

        var failure = Assert.Throws<InvalidOperationException>(() => GeneratedFiles.Check(directory, expected));

        Assert.Contains("documents are stale; rebuild and commit", failure.Message);
        Assert.Equal(before.Keys.Order(), Directory.GetFiles(directory).Order());
        Assert.All(before, pair => Assert.Equal(pair.Value, File.ReadAllText(pair.Key)));
    }

    [Theory]
    [InlineData("../handwritten.md")]
    [InlineData("..\\handwritten.md")]
    [InlineData("/handwritten.md")]
    [InlineData("index.md:stream.md")]
    public void An_invalid_manifest_cannot_delete_files_outside_the_generated_set(string entry)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, ".controlbatyr-generated"), entry + "\n");

        var failure = Assert.Throws<InvalidOperationException>(() => GeneratedFiles.Write(directory, [("index.md", "Index\n")]));

        Assert.Contains("Invalid generated document name", failure.Message);
        Assert.False(File.Exists(Path.Combine(directory, "index.md")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_executable_reports_load_failures_or_all_rule_violations_without_changing_documents(bool invalidCore)
    {
        GeneratedFiles.Write(directory, [("index.md", "Old index\n")]);
        var assemblyList = Path.Combine(directory, "assemblies.txt");
        var referenceList = Path.Combine(directory, "references.txt");
        File.WriteAllText(assemblyList, invalidCore ? typeof(RulesTests).Assembly.Location : Path.Combine(directory, "missing-core.dll"));
        File.WriteAllLines(referenceList, Directory.GetFiles(AppContext.BaseDirectory, "*.dll")
            .Concat(Directory.GetFiles(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "*.dll")));
        var testAssembly = typeof(GeneratedFilesTests).Assembly.Location;
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(testAssembly, ".runtimeconfig.json"),
            "--depsfile", Path.ChangeExtension(testAssembly, ".deps.json"), typeof(BusinessApiReader).Assembly.Location,
            directory, assemblyList, referenceList, invalidCore ? typeof(Rules).Assembly.Location : "", "false", "markdown;model" })
        {
            start.ArgumentList.Add(argument);
        }
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);

        Assert.NotEqual(0, process.ExitCode);
        var diagnostics = await error;
        if (invalidCore)
        {
            foreach (var code in Enumerable.Range(1, 7))
            {
                Assert.Contains($": error CB{code:0000}:", diagnostics);
            }
        }
        else
        {
            Assert.Contains("ControlBatyr business API generation failed:", diagnostics);
        }
        Assert.Empty(await output);
        Assert.Equal("Old index\n", File.ReadAllText(Path.Combine(directory, "index.md")));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
