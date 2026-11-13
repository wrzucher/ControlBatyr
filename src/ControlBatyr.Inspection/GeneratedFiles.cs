namespace ControlBatyr.Inspection;

internal static class GeneratedFiles
{
    private const string Manifest = ".controlbatyr-generated";

    internal static void Check(string directory, IReadOnlyList<(string RelativePath, string Content)> documents,
        Func<string, bool>? isEnabled = null)
    {
        isEnabled ??= _ => true;
        var manifest = Path.Combine(directory, Manifest);
        var previous = File.Exists(manifest) ? File.ReadAllLines(manifest).Where(isEnabled).ToArray() : [];
        var expected = documents.Append((Manifest, string.Join('\n', documents.Select(document => document.RelativePath)
            .Order(StringComparer.Ordinal)) + "\n"));
        foreach (var (file, content) in expected)
        {
            var path = Path.Combine(directory, file);
            var actual = file == Manifest ? string.Join('\n', previous) + "\n"
                : File.Exists(path) ? File.ReadAllText(path).ReplaceLineEndings("\n") : null;
            if (!File.Exists(path) || actual != content)
            {
                throw new InvalidOperationException("ControlBatyr documents are stale; rebuild and commit the generated documents and ownership manifest.");
            }
        }
    }

    internal static void Write(string directory, IReadOnlyList<(string RelativePath, string Content)> documents,
        Func<string, bool>? isEnabled = null)
    {
        isEnabled ??= _ => true;
        var manifest = Path.Combine(directory, Manifest);
        var previous = File.Exists(manifest) ? File.ReadAllLines(manifest) : [];
        var current = documents.Select(document => document.RelativePath).ToArray();
        foreach (var file in previous.Concat(current))
        {
            if (string.IsNullOrWhiteSpace(file) || file.IndexOfAny(['/', '\\', ':']) >= 0
                || !Outputs.IsKnownFile(file) || file is ".md" or "..md")
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
        foreach (var (file, content) in documents)
        {
            WriteIfChanged(Path.Combine(directory, file), content);
        }
        foreach (var file in previous.Where(isEnabled).Except(current, StringComparer.OrdinalIgnoreCase))
        {
            File.Delete(Path.Combine(directory, file));
        }
        var owned = current.Concat(previous.Where(file => !isEnabled(file))).Order(StringComparer.Ordinal);
        WriteIfChanged(manifest, string.Join('\n', owned) + "\n");
    }

    private static void WriteIfChanged(string path, string content)
    {
        if (!File.Exists(path) || File.ReadAllText(path).ReplaceLineEndings("\n") != content)
        {
            File.WriteAllText(path, content);
        }
    }
}
