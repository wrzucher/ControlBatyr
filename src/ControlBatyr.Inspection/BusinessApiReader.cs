using System.Reflection;

namespace ControlBatyr.Inspection;

/// <summary>Reads the external business API shape and adjacent XML comments and portable PDBs.</summary>
public static class BusinessApiReader
{
    /// <summary>Source paths are relative to documentDirectory; null omits source paths.</summary>
    public static BusinessApiModel Read(string? documentDirectory, params Assembly[] assemblies)
    {
        return new AssemblyReader(assemblies, documentDirectory).Read();
    }
}
