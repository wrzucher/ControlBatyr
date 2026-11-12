using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ControlBatyr.Inspection;

internal sealed class XmlComments
{
    private readonly Dictionary<Assembly, Dictionary<string, XElement>> documents;

    internal XmlComments(IEnumerable<Assembly> assemblies)
    {
        documents = assemblies.ToDictionary(assembly => assembly, assembly =>
        {
            var path = Path.ChangeExtension(assembly.Location, ".xml");
            return File.Exists(path)
                ? XDocument.Load(path).Descendants("member").ToDictionary(member => (string)member.Attribute("name")!, member => member)
                : new Dictionary<string, XElement>();
        });
    }

    internal string Summary(MemberInfo member)
    {
        var document = documents.GetValueOrDefault(member.Module.Assembly);
        var summary = document?.GetValueOrDefault(Id(member))?.Element("summary");
        if (summary is null && member is PropertyInfo)
        {
            summary = document?.GetValueOrDefault(Id(member.DeclaringType!))?.Elements("param")
                .FirstOrDefault(param => (string?)param.Attribute("name") == member.Name);
        }
        var text = summary is null ? "" : Regex.Replace(Content(summary), @"\s+", " ").Trim();
        return text.Length == 0 ? "—" : text.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("|", "&#124;", StringComparison.Ordinal);
    }

    private static string Content(XElement element) => string.Concat(element.Nodes().Select(node => node switch
    {
        XText text => text.Value,
        XElement child when child.Name.LocalName is "see" or "seealso" => child.Value.Length > 0 ? child.Value
            : (string?)child.Attribute("langword") ?? ((string?)child.Attribute("cref"))?[2..] ?? "",
        XElement child when child.Name.LocalName is "paramref" or "typeparamref" => (string?)child.Attribute("name") ?? "",
        XElement child => " " + Content(child) + " ",
        _ => ""
    }));

    internal static string Id(MemberInfo member)
    {
        if (member is Type type)
        {
            return "T:" + TypeId(type);
        }
        var prefix = member is MethodInfo ? "M:" : member is PropertyInfo ? "P:" : member is FieldInfo ? "F:" : "E:";
        var name = prefix + TypeId(member.DeclaringType!) + "." + member.Name;
        if (member is MethodInfo method)
        {
            if (method.IsGenericMethod)
            {
                name += "``" + method.GetGenericArguments().Length;
            }
            var parameters = method.GetParameters();
            if (parameters.Length > 0)
            {
                name += "(" + string.Join(",", parameters.Select(parameter => TypeId(parameter.ParameterType))) + ")";
            }
        }
        return name;
    }

    private static string TypeId(Type type)
    {
        if (type.IsByRef)
        {
            return TypeId(type.GetElementType()!) + "@";
        }
        if (type.IsPointer)
        {
            return TypeId(type.GetElementType()!) + "*";
        }
        if (type.IsArray)
        {
            return TypeId(type.GetElementType()!) + (type.GetArrayRank() == 1 ? "[]"
                : "[" + string.Join(",", Enumerable.Repeat("0:", type.GetArrayRank())) + "]");
        }
        if (type.IsGenericParameter)
        {
            return (type.DeclaringMethod is null ? "`" : "``") + type.GenericParameterPosition;
        }
        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            return Regex.Replace(type.GetGenericTypeDefinition().FullName!.Replace('+', '.'), @"`\d+", "")
                + "{" + string.Join(",", type.GetGenericArguments().Select(TypeId)) + "}";
        }
        return (type.FullName ?? type.Name).Replace('+', '.');
    }
}
