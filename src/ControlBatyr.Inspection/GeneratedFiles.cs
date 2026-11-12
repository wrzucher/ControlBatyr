namespace ControlBatyr.Inspection;

internal static class GeneratedFiles
{
    private const string Manifest = ".controlbatyr-generated";

    internal static void Write(string directory, IReadOnlyList<(string RelativePath, string Markdown)> documents)
    {
        var manifest = Path.Combine(directory, Manifest);
        var previous = File.Exists(manifest) ? File.ReadAllLines(manifest) : [];
        var current = documents.Select(document => document.RelativePath).ToArray();
        foreach (var file in previous.Concat(current))
        {
            if (string.IsNullOrWhiteSpace(file) || file.IndexOfAny(['/', '\\', ':']) >= 0
                || !file.EndsWith(".md", StringComparison.Ordinal) || file is ".md" or "..md")
            {
                throw new InvalidOperationException($"Invalid generated document name: {file}.");
            }
        }
        foreach (var file in current.Except(previous, StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(Path.Combine(directory, file)))
            {
                throw new InvalidOperationException($"Refusing to overwrite unowned document: {file}. Move it or choose another documentation folder.");
            }
        }
        Directory.CreateDirectory(directory);
        foreach (var (file, markdown) in documents)
        {
            WriteIfChanged(Path.Combine(directory, file), markdown);
        }
        foreach (var file in previous.Except(current, StringComparer.OrdinalIgnoreCase))
        {
            File.Delete(Path.Combine(directory, file));
        }
        WriteIfChanged(manifest, string.Join('\n', current.Order(StringComparer.Ordinal)) + "\n");
    }

    private static void WriteIfChanged(string path, string content)
    {
        if (!File.Exists(path) || File.ReadAllText(path).ReplaceLineEndings("\n") != content)
        {
            File.WriteAllText(path, content);
        }
    }
}
