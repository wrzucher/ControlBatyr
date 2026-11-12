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
        var directory = DocumentDirectory();
        var documents = BusinessApi.Generate(directory, typeof(GameManager).Assembly, typeof(Bet).Assembly);
        Directory.CreateDirectory(directory);
        var changed = false;
        foreach (var (relativePath, markdown) in documents)
        {
            var path = Path.Combine(directory, relativePath);
            var previous = File.Exists(path) ? File.ReadAllText(path).ReplaceLineEndings("\n") : null;
            if (previous != markdown)
            {
                File.WriteAllText(path, markdown);
                changed = true;
            }
        }
        foreach (var path in Directory.GetFiles(directory, "*.md"))
        {
            if (!documents.Any(document => document.RelativePath == Path.GetFileName(path)))
            {
                File.Delete(path);
                changed = true;
            }
        }
        Assert.False(changed, "The business API documents were updated at sample/docs/business-api/ and must be committed.");
    }

    private static string DocumentDirectory([CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "../docs/business-api"));
}
