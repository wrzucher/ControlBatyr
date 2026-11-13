using System.Text;

namespace ControlBatyr.Inspection;

internal static class MermaidRenderer
{
    internal static string Render(BusinessApiModel model, BusinessDomain domain)
    {
        var output = new StringBuilder("classDiagram\n%% @arch/1 diagram profile=class-v1\n");
        var keys = new Dictionary<string, string>(StringComparer.Ordinal);
        var models = model.Models.ToDictionary(item => item.Node.Id);
        var ownEvents = domain.Publishers.SelectMany(publisher => publisher.Events).Concat(domain.RecordEvents).ToArray();
        var eventIds = ownEvents.Select(item => item.Node.Id).Concat(domain.Operations.SelectMany(operation => operation.Raises))
            .ToHashSet(StringComparer.Ordinal);
        var connections = model.Connections.Where(connection => connection.TargetDomainId == domain.Node.Id
            || eventIds.Contains(connection.EventId)).ToArray();
        var events = ownEvents.Concat(model.Domains.SelectMany(item => item.Publishers.SelectMany(publisher => publisher.Events)
            .Concat(item.RecordEvents)).Where(item => eventIds.Contains(item.Node.Id)
                || connections.Any(connection => connection.EventId == item.Node.Id)))
            .DistinctBy(item => item.Node.Id).ToArray();
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var operation in domain.Operations)
        {
            Discover(operation.ReturnType);
            foreach (var parameter in operation.Parameters)
            {
                Discover(parameter.Type);
            }
        }
        var signatureModels = domain.Operations.SelectMany(operation => operation.Parameters.Select(parameter => parameter.Type)
            .Prepend(operation.ReturnType)).SelectMany(References).Distinct().ToArray();
        foreach (var item in events)
        {
            foreach (var payload in item.Payloads)
            {
                Discover(payload);
            }
        }
        Class(domain.Node, domain.Node.Name, null, domain.Operations.Select(operation => Name(operation.Node.Name) + "()"));
        foreach (var item in model.Models.Where(item => used.Contains(item.Node.Id) && !events.Any(e => e.Node.Id == item.Node.Id)))
        {
            Class(item.Node, item.Node.Name, item.IsEnum ? "enumeration" : item.IsRecord ? "record" : null,
                item.IsEnum ? item.Values.Select(value => value.Node.Name) : Properties(item));
        }
        foreach (var item in events)
        {
            var properties = models.TryGetValue(item.Node.Id, out var record) ? Properties(record)
                : item.Payloads.SelectMany(payload => payload.ModelId is { } id && models.TryGetValue(id, out var data)
                    ? Properties(data) : new[] { TypeName(payload) + " payload" });
            Class(item.Node, item.Node.Name, "event", properties);
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

        void Discover(TypeReference type)
        {
            foreach (var argument in type.Arguments)
            {
                Discover(argument);
            }
            if (type.ModelId is not { } id || !used.Add(id))
            {
                return;
            }
            foreach (var property in models[id].Properties)
            {
                Discover(property.Type);
            }
            foreach (var typeReference in models[id].Methods.SelectMany(operation => operation.Parameters.Select(parameter => parameter.Type)
                .Prepend(operation.ReturnType)))
            {
                Discover(typeReference);
            }
        }

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

    private static IEnumerable<string> Properties(BusinessModel model) =>
        model.Properties.Select(property => TypeName(property.Type) + " " + Name(property.Node.Name));

    private static string TypeName(TypeReference type)
    {
        if (type.Name.Length == 0)
        {
            return TypeName(type.Arguments.Single()) + type.Suffix;
        }
        // Mermaid cannot represent generic argument lists containing commas or tuple member syntax.
        if (type.Name == "(")
        {
            return "Tuple" + type.Suffix;
        }
        return Name(type.Name) + (type.Arguments.Length == 1 ? "~" + TypeName(type.Arguments[0]) + "~" : "") + type.Suffix;
    }

    private static string Name(string name) => new(name.TakeWhile(character => character != '&' && character != '`')
        .Select(character => char.IsLetterOrDigit(character) || character == '_' ? character : '_').ToArray());

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"")
        .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
}
