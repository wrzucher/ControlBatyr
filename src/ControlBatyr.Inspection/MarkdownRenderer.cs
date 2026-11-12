using System.Collections.Immutable;
using System.Text;

namespace ControlBatyr.Inspection;

internal sealed class MarkdownRenderer(BusinessApiModel model)
{
    private readonly StringBuilder output = new();
    private readonly Dictionary<string, DocumentLocation> locations = Locations(model);
    private string currentFile = "index.md";

    internal sealed record DocumentLocation(string Id, string File, string Anchor);

    internal static Dictionary<string, DocumentLocation> Locations(BusinessApiModel model)
    {
        var result = new Dictionary<string, DocumentLocation>();
        var models = model.Models.ToDictionary(item => item.Node.Id);
        var owners = models.Keys.ToDictionary(id => id, _ => new HashSet<string>());
        var files = new HashSet<string>(StringComparer.Ordinal) { "index.md", "shared.md" };
        foreach (var domain in model.Domains)
        {
            var file = domain.Name.ToLowerInvariant() + ".md";
            if (!files.Add(file))
            {
                throw new InvalidOperationException($"Domain document path is not unique: {file}.");
            }
            Add(domain.Node, file, TypeAnchor(domain.Node.Id));
            Operations(domain.Operations, file);
            foreach (var publisher in domain.Publishers)
            {
                Add(publisher.Node, file, TypeAnchor(publisher.Node.Id));
                foreach (var item in publisher.Events)
                {
                    Add(item.Node, file, MemberAnchor(item.Node.Id));
                    foreach (var payload in item.Payloads)
                    {
                        Discover(payload, file);
                    }
                }
            }
            foreach (var operation in domain.Operations)
            {
                DiscoverOperation(operation, file);
            }
        }
        foreach (var item in model.Models)
        {
            var file = owners[item.Node.Id].Count == 1 ? owners[item.Node.Id].Single() : "shared.md";
            var anchor = TypeAnchor(item.Node.Id);
            Add(item.Node, file, anchor);
            foreach (var node in item.Properties.Select(property => property.Node).Concat(item.Values.Select(value => value.Node)))
            {
                Add(node, file, anchor);
            }
            Operations(item.Methods, file);
        }
        foreach (var connection in model.Connections)
        {
            var target = result[connection.TargetDomainId];
            Add(connection.Node, target.File, target.Anchor);
        }
        return result;

        void Add(BusinessNode node, string file, string anchor)
        {
            result.TryAdd(node.Id, new(node.Id, file, anchor));
        }

        void Operations(ImmutableArray<BusinessOperation> operations, string file)
        {
            foreach (var operation in operations)
            {
                var anchor = MemberAnchor(operation.Node.Id);
                if (operations.Count(item => MemberAnchor(item.Node.Id) == anchor) > 1)
                {
                    var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(operation.Node.Id));
                    anchor += "-" + Convert.ToHexStringLower(hash)[..8];
                }
                Add(operation.Node, file, anchor);
                foreach (var requirement in operation.Requirements)
                {
                    Add(requirement.Node, file, anchor);
                }
            }
        }

        void DiscoverOperation(BusinessOperation operation, string file)
        {
            Discover(operation.ReturnType, file);
            foreach (var parameter in operation.Parameters)
            {
                Discover(parameter.Type, file);
            }
        }

        void Discover(TypeReference reference, string file)
        {
            foreach (var argument in reference.Arguments)
            {
                Discover(argument, file);
            }
            if (reference.ModelId is not { } id || !owners[id].Add(file))
            {
                return;
            }
            var item = models[id];
            foreach (var property in item.Properties)
            {
                Discover(property.Type, file);
            }
            foreach (var method in item.Methods)
            {
                DiscoverOperation(method, file);
            }
        }
    }

    private static string TypeAnchor(string id)
    {
        return "type-" + id[(id.IndexOf('|') + 3)..].Replace('.', '-').Replace('+', '-').Replace('`', '-').ToLowerInvariant();
    }

    private static string MemberAnchor(string id)
    {
        var signature = id.Split('(')[0];
        var separator = signature.LastIndexOf('.');
        return TypeAnchor(signature[..separator]) + "-" + signature[(separator + 1)..].Split('`')[0].ToLowerInvariant();
    }

    internal IReadOnlyList<(string RelativePath, string Markdown)> Render()
    {
        var documents = new List<(string RelativePath, string Markdown)>();
        Start("index.md", "Business API");
        Text("## Domains");
        foreach (var domain in model.Domains)
        {
            Text($"- [{domain.Name}]({File(domain.Node)}) — {domain.Node.Summary}");
        }
        Connections(model.Connections);
        documents.Add(Finish());
        foreach (var domain in model.Domains)
        {
            Start(File(domain.Node), domain.Name);
            Text("[Business API](index.md)");
            Text("## Operations");
            Heading(domain.Node);
            Methods(domain.Operations);
            if (domain.Publishers.Length > 0)
            {
                Text("## Events");
                foreach (var publisher in domain.Publishers)
                {
                    Heading(publisher.Node);
                    foreach (var item in publisher.Events)
                    {
                        MemberHeading(item.Node);
                        if (item.Payloads.Length > 0)
                        {
                            Text($"**Payload:** {string.Join(", ", item.Payloads.Select(TypeName))}");
                        }
                        Remarks(item.Node);
                    }
                }
            }
            Models(currentFile);
            Connections(model.Connections.Where(connection => connection.TargetDomainId == domain.Node.Id
                || domain.Publishers.Any(publisher => publisher.Events.Any(item => item.Node.Id == connection.EventId))));
            documents.Add(Finish());
        }
        if (model.Models.Any(item => File(item.Node) == "shared.md"))
        {
            Start("shared.md", "Shared models");
            Text("[Business API](index.md)");
            Models("shared.md");
            documents.Add(Finish());
        }
        return documents;
    }

    private string File(BusinessNode node)
    {
        return locations[node.Id].File;
    }

    private void Start(string file, string title)
    {
        output.Clear();
        currentFile = file;
        Text($"# {title}");
    }

    private (string RelativePath, string Markdown) Finish()
    {
        return (currentFile, output.ToString().TrimEnd() + "\n");
    }

    private string Link(string label, string id)
    {
        var location = locations[id];
        return $"[{label}]({(location.File == currentFile ? "" : location.File)}#{location.Anchor})";
    }

    private void Connections(IEnumerable<BusinessConnection> source)
    {
        var items = source.ToArray();
        if (items.Length == 0)
        {
            return;
        }
        Text("## Connections");
        foreach (var connection in items)
        {
            var from = model.Domains.Single(domain => domain.Publishers.Any(publisher =>
                publisher.Events.Any(item => item.Node.Id == connection.EventId)));
            var to = model.Domains.Single(domain => domain.Node.Id == connection.TargetDomainId);
            var item = from.Publishers.SelectMany(publisher => publisher.Events).Single(item => item.Node.Id == connection.EventId);
            var action = connection.Node.Name;
            if (action.EndsWith("Async", StringComparison.Ordinal))
            {
                action = action[..^5];
            }
            Text($"- {Link(from.Name + "." + item.Node.Name, connection.EventId)} → "
                + $"[{to.Name}]({File(to.Node)}): {action} — {connection.Node.Summary}");
        }
    }

    private void Models(string file)
    {
        var items = model.Models.Where(item => File(item.Node) == file).ToArray();
        if (items.Length == 0)
        {
            return;
        }
        Text("## Models");
        foreach (var item in items)
        {
            Heading(item.Node);
            if (item.IsEnum)
            {
                Enum(item.Values);
            }
            else
            {
                Properties("Data", item.Properties.Where(property => !property.Computed).ToArray());
                Properties("Computed properties", item.Properties.Where(property => property.Computed).ToArray());
                Methods(item.Methods);
            }
        }
    }

    private void Enum(ImmutableArray<BusinessEnumValue> values)
    {
        var described = values.Any(value => value.Node.Summary.Length > 0);
        Line(described ? "| Value | Description |" : "| Value |");
        Line(described ? "| --- | --- |" : "| --- |");
        foreach (var value in values)
        {
            Line($"| {value.Node.Name} = {value.Value} |" + (described ? $" {value.Node.Summary} |" : ""));
        }
        Line();
    }

    private void Properties(string title, BusinessProperty[] properties)
    {
        if (properties.Length == 0)
        {
            return;
        }
        Text($"#### {title}");
        var described = properties.Any(property => property.Node.Summary.Length > 0);
        TableHeader(described);
        foreach (var property in properties)
        {
            Line($"| {property.Node.Name} | {TypeName(property.Type)} |" + (described ? $" {property.Node.Summary} |" : ""));
        }
        Line();
    }

    private void Heading(BusinessNode node)
    {
        Anchor(node);
        Text($"### {node.Name}");
        Text(SourceLink(node));
        Text(node.Summary);
        Remarks(node);
    }

    private void MemberHeading(BusinessNode node)
    {
        Anchor(node);
        Text($"#### {node.Name}");
        Text(node.Summary);
    }

    private void Anchor(BusinessNode node)
    {
        Text($"<a id=\"{locations[node.Id].Anchor}\"></a>");
    }

    private static string SourceLink(BusinessNode node)
    {
        return node.SourceFile is null ? "" : $"[Source]({string.Join("/", node.SourceFile.Split('/').Select(Uri.EscapeDataString))})";
    }

    private void Methods(ImmutableArray<BusinessOperation> methods)
    {
        foreach (var method in methods)
        {
            MemberHeading(method.Node);
            if (method.Parameters.Length > 0)
            {
                Text("**Parameters**");
                var described = method.Parameters.Any(parameter => parameter.Description.Length > 0);
                TableHeader(described);
                foreach (var parameter in method.Parameters)
                {
                    Line($"| {parameter.Name} | {TypeName(parameter.Type)} |" + (described ? $" {parameter.Description} |" : ""));
                }
                Line();
            }
            if (method.ReturnType.Name != "void" || method.Returns.Length > 0)
            {
                Text(method.ReturnType.Name == "void" ? "**Returns**" : $"**Returns:** {TypeName(method.ReturnType)}");
                Text(method.Returns);
            }
            Remarks(method.Node);
            if (method.Requirements.Length > 0)
            {
                Text("**Requirements**");
                foreach (var requirement in method.Requirements)
                {
                    var node = requirement.Node;
                    var text = node.Summary.Length > 0 ? node.Summary : node.Name.Replace('_', ' ');
                    var source = SourceLink(node);
                    Line($"- {text}" + (source.Length == 0 ? "" : $" — {source}"));
                }
                Line();
            }
        }
    }

    private void TableHeader(bool described)
    {
        Line(described ? "| Name | Type | Description |" : "| Name | Type |");
        Line(described ? "| --- | --- | --- |" : "| --- | --- |");
    }

    private void Remarks(BusinessNode node)
    {
        if (node.Remarks.Length > 0)
        {
            Text("**Remarks**");
            Text(node.Remarks);
        }
    }

    private string TypeName(TypeReference type)
    {
        if (type.Name.Length == 0)
        {
            return TypeName(type.Arguments.Single()) + type.Suffix;
        }
        var name = type.ModelId is null ? type.Name : Link(type.Name, type.ModelId);
        if (type.Arguments.Length > 0)
        {
            name += "&lt;" + string.Join(", ", type.Arguments.Select(TypeName)) + "&gt;";
        }
        return name + type.Suffix;
    }

    private void Text(string text)
    {
        if (text.Length > 0)
        {
            Line(text);
            Line();
        }
    }

    private void Line(string text = "")
    {
        output.Append(text).Append('\n');
    }
}
