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

    [Fact]
    public void A_new_document_cannot_overwrite_a_handwritten_file_and_failure_preserves_the_old_set()
    {
        GeneratedFiles.Write(directory, [("index.md", "Old index\n")]);
        File.WriteAllText(Path.Combine(directory, "domain.md"), "Handwritten domain\n");

        var failure = Assert.Throws<InvalidOperationException>(() => GeneratedFiles.Write(directory,
            [("index.md", "New index\n"), ("domain.md", "Generated domain\n")]));

        Assert.Contains("unowned document: domain.md", failure.Message);
        Assert.Equal("Handwritten domain\n", File.ReadAllText(Path.Combine(directory, "domain.md")));
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

    [Fact]
    public async Task The_executable_reports_unreadable_assemblies_and_exits_unsuccessfully_without_changing_documents()
    {
        GeneratedFiles.Write(directory, [("index.md", "Old index\n")]);
        var assemblyList = Path.Combine(directory, "assemblies.txt");
        var referenceList = Path.Combine(directory, "references.txt");
        File.WriteAllText(assemblyList, Path.Combine(directory, "missing-core.dll"));
        File.WriteAllText(referenceList, "");
        var testAssembly = typeof(GeneratedFilesTests).Assembly.Location;
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(testAssembly, ".runtimeconfig.json"),
            "--depsfile", Path.ChangeExtension(testAssembly, ".deps.json"), typeof(BusinessApi).Assembly.Location,
            directory, assemblyList, referenceList })
        {
            start.ArgumentList.Add(argument);
        }
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);

        Assert.NotEqual(0, process.ExitCode);
        Assert.Contains("ControlBatyr business API generation failed:", await error);
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
