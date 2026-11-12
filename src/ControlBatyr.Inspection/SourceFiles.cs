using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace ControlBatyr.Inspection;

internal static class SourceFiles
{
    internal static string? Find(MemberInfo member)
    {
        var type = member as Type ?? member.DeclaringType!;
        var path = Path.ChangeExtension(type.Assembly.Location, ".pdb");
        if (!File.Exists(path))
        {
            return null;
        }
        using var assemblyStream = File.OpenRead(type.Assembly.Location);
        using var pe = new PEReader(assemblyStream);
        var codeView = pe.ReadDebugDirectory().FirstOrDefault(entry => entry.Type == DebugDirectoryEntryType.CodeView);
        if (codeView.DataSize == 0)
        {
            return null;
        }
        using var stream = File.OpenRead(path);
        using var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
        var reader = provider.GetMetadataReader();
        // A stale PDB can map valid tokens to the wrong source file.
        if (new BlobContentId(reader.DebugMetadataHeader!.Id).Guid != pe.ReadCodeViewDebugDirectoryData(codeView).Guid)
        {
            return null;
        }
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Cast<MethodBase>().Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            .OrderBy(method => method == member ? 0 : 1).ThenBy(method => method.MetadataToken);
        foreach (var method in methods)
        {
            var handle = (MethodDefinitionHandle)MetadataTokens.Handle(method.MetadataToken);
            var debug = reader.GetMethodDebugInformation(handle);
            foreach (var point in debug.GetSequencePoints())
            {
                var document = point.Document.IsNil ? debug.Document : point.Document;
                if (!point.IsHidden && !document.IsNil)
                {
                    return reader.GetString(reader.GetDocument(document).Name);
                }
            }
        }
        return null;
    }
}
