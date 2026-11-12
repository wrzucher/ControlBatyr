using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ControlBatyr.Inspection;

internal sealed class XmlComments
{
    private readonly Dictionary<Assembly, Dictionary<string, XElement>> documents = [];

    private Dictionary<string, XElement> Document(Assembly assembly)
    {
        if (!documents.TryGetValue(assembly, out var document))
        {
            var path = Path.ChangeExtension(assembly.Location, ".xml");
            document = File.Exists(path)
                ? XDocument.Load(path).Descendants("member").ToDictionary(member => (string)member.Attribute("name")!, member => member)
                : new Dictionary<string, XElement>();
            documents.Add(assembly, document);
        }
        return document;
    }

    internal string Summary(MemberInfo member)
    {
        var summary = Tag(member, "summary");
        if (summary.Length == 0 && member is PropertyInfo)
        {
            summary = Tag(member.DeclaringType!, "param", member.Name);
        }
        return summary;
    }

    internal string Tag(MemberInfo member, string tag, string? name = null)
    {
        var element = Document(member.Module.Assembly).GetValueOrDefault(Id(member))?.Elements(tag)
            .FirstOrDefault(item => name is null || (string?)item.Attribute("name") == name);
        var text = element is null ? "" : Regex.Replace(Content(element), @"\s+", " ").Trim();
        return text.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("|", "&#124;", StringComparison.Ordinal);
    }

    private static string Content(XElement element) => string.Concat(element.Nodes().Select(node => node switch
    {
        XText text => text.Value,
        XElement child when child.Name.LocalName is "see" or "seealso" => child.Value.Length > 0 ? child.Value
            : (string?)child.Attribute("langword") ?? ((string?)child.Attribute("cref"))?[2..]
                ?? (string?)child.Attribute("href") ?? "",
        XElement child when child.Name.LocalName is "paramref" or "typeparamref" => (string?)child.Attribute("name") ?? "",
        XElement child when child.Name.LocalName is "para" or "c" => " " + Content(child) + " ",
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
