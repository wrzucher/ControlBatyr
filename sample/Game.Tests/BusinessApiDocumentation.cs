using System.Runtime.CompilerServices;
using ControlBatyr.Inspection;
using Game.Core;
using Game.Model;
using Xunit;

namespace Game.Tests;

public sealed class BusinessApiDocumentation
{
    [Fact]
    public void Business_api_document_is_current()
    {
        var path = DocumentPath();
        var markdown = BusinessApi.Generate(path, typeof(GameManager).Assembly, typeof(Bet).Assembly);
        var previous = File.Exists(path) ? File.ReadAllText(path).ReplaceLineEndings("\n") : null;
        if (previous == markdown)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, markdown);
        Assert.Fail("The business API document was updated at sample/docs/business-api.md and must be committed.");
    }

    private static string DocumentPath([CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "../docs/business-api.md"));
}
