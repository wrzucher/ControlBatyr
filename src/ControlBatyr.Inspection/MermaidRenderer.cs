using System.Text;

namespace ControlBatyr.Inspection;

internal static class MermaidRenderer
{
    internal static string Render(BusinessApiModel model, BusinessDomain domain)
    {
        var output = new StringBuilder("classDiagram\n%% @arch/1 diagram profile=class-v1\n");
        var keys = new Dictionary<string, string>(StringComparer.Ordinal);
        var ownEvents = domain.Publishers.SelectMany(publisher => publisher.Events).Concat(domain.RecordEvents).ToArray();
        var eventIds = ownEvents.Select(item => item.Node.Id).Concat(domain.Operations.SelectMany(operation => operation.Raises))
            .ToHashSet(StringComparer.Ordinal);
        var connections = model.Connections.Where(connection => connection.TargetDomainId == domain.Node.Id
            || eventIds.Contains(connection.EventId)).ToArray();
        var events = ownEvents.Concat(model.Domains.SelectMany(item => item.Publishers.SelectMany(publisher => publisher.Events)
            .Concat(item.RecordEvents)).Where(item => eventIds.Contains(item.Node.Id)
                || connections.Any(connection => connection.EventId == item.Node.Id)))
            .DistinctBy(item => item.Node.Id).ToArray();
        var signatureModels = domain.Operations.SelectMany(operation => operation.Parameters.Select(parameter => parameter.Type)
            .Prepend(operation.ReturnType)).SelectMany(References).ToHashSet(StringComparer.Ordinal);
        Class(domain.Node, domain.Node.Name, null, domain.Operations.Select(operation => Name(operation.Node.Name) + "()"));
        foreach (var item in model.Models.Where(item => signatureModels.Contains(item.Node.Id) && !events.Any(e => e.Node.Id == item.Node.Id)))
        {
            Class(item.Node, item.Node.Name, item.IsEnum ? "enumeration" : item.IsRecord ? "record" : null,
                []);
        }
        foreach (var item in events)
        {
            Class(item.Node, item.Node.Name, "event", []);
        }
        foreach (var target in model.Domains.Where(target => target.Node.Id != domain.Node.Id
            && connections.Any(connection => connection.TargetDomainId == target.Node.Id)))
        {
            Class(target.Node, target.Name, null, []);
        }
        foreach (var id in signatureModels)
        {
            Edge(domain.Node.Id, id);
        }
        foreach (var operation in domain.Operations)
        {
            foreach (var id in operation.Raises)
            {
                Edge(domain.Node.Id, id, "raises " + Name(operation.Node.Name));
            }
        }
        foreach (var connection in connections)
        {
            Edge(connection.EventId, connection.TargetDomainId, "on");
        }
        return output.ToString();

        void Class(BusinessNode node, string label, string? stereotype, IEnumerable<string> members)
        {
            var key = "C" + keys.Count;
            keys.Add(node.Id, key);
            output.Append("%% @arch/1 type id=\"").Append(Escape(node.Id)).Append("\"\n");
            output.Append("class ").Append(key).Append("[\"").Append(Name(label)).Append("\"] {\n");
            if (stereotype is not null)
            {
                output.Append("    <<").Append(stereotype).Append(">>\n");
            }
            foreach (var member in members)
            {
                output.Append("    +").Append(member).Append('\n');
            }
            output.Append("}\n");
        }

        void Edge(string from, string to, string? label = null)
        {
            output.Append(keys[from]).Append(" ..> ").Append(keys[to]);
            if (label is not null)
            {
                output.Append(" : ").Append(label);
            }
            output.Append('\n');
        }
    }

    private static IEnumerable<string> References(TypeReference type) =>
        (type.ModelId is { } id ? new[] { id } : []).Concat(type.Arguments.SelectMany(References));

    private static string Name(string name) => new(name.TakeWhile(character => character != '&' && character != '`')
        .Select(character => char.IsLetterOrDigit(character) || character == '_' ? character : '_').ToArray());

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"")
        .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
}
