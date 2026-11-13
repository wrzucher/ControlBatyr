using System.Runtime.CompilerServices;
using System.Text.Json;
using ControlBatyr.Inspection;
using Xunit;

namespace ControlBatyr.Tests;

public sealed class BusinessModelTests
{
    private static string SampleDirectory([CallerFilePath] string file = "")
    {
        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, "../../sample/docs/business-api"));
    }

    private static BusinessApiModel ReadSample()
    {
        var assemblies = ProductTypes.Find(typeof(Game.Tests.GameRequirements).Assembly)
            .Select(type => type.Assembly).Distinct().ToArray();
        return BusinessApiReader.Read(SampleDirectory(), assemblies);
    }

    [Fact]
    public void Sample_model_connects_operations_events_requirements_and_reachable_data()
    {
        var model = ReadSample();
        var game = model.Domains.Single(domain => domain.Name == "Game");
        var cancel = game.Operations.Single(operation => operation.Node.Name == "CancelBet");
        Assert.Equal("A:Game.Core|M:Game.Core.GameManager.CancelBetAsync(System.Guid,System.Threading.CancellationToken)", cancel.Node.Id);
        Assert.Contains("Listeners are notified", cancel.Node.Remarks);
        Assert.Contains(cancel.Requirements, requirement => requirement.Node.Name == "Open_bet_can_be_cancelled_before_the_game_starts");
        var cancelled = game.Publishers.Single().Events.Single(item => item.Node.Name == "BetCancelled");
        Assert.Equal("A:Game.Model|T:Game.Model.BetCancelledEventArgs", cancelled.Payloads.Single().ModelId);
        var connection = Assert.Single(model.Connections);
        Assert.Equal(cancelled.Node.Id, connection.EventId);
        Assert.Equal(model.Domains.Single(domain => domain.Name == "Account").Node.Id, connection.TargetDomainId);
        var bet = model.Models.Single(item => item.Node.Name == "Bet");
        Assert.True(bet.Properties.Single(property => property.Node.Name == "IsOpen").Computed);
        Assert.Contains(bet.Properties, property => property.Node.Id == "A:Game.Model|P:Game.Model.Bet.State"
            && property.Type.ModelId == "A:Game.Model|T:Game.Model.BetState");
        Assert.Contains(model.Models.Single(item => item.Node.Name == "BetState").Values,
            value => value.Node.Id == "A:Game.Model|F:Game.Model.BetState.Cancelled" && value.Value == "1");
        Assert.DoesNotContain(model.Models, item => item.Node.Name.EndsWith("Persistence", StringComparison.Ordinal));
    }

    [Fact]
    public void Overview_lists_business_summaries_and_links_to_reference_details_in_section_order()
    {
        var model = ReadSample();
        var domain = model.Domains.Single(item => item.Name == "Game");
        var documents = BusinessApiMarkdown.Render(model).ToDictionary(item => item.RelativePath, item => item.Markdown);
        var overview = documents["game.md"];
        Assert.StartsWith("# Game\n\n" + domain.Node.Summary + "\n", overview);
        Assert.Contains("[Reference](game.reference.md)", overview);
        var locations = MarkdownRenderer.Locations(model);
        var nodes = domain.Operations.Select(item => item.Node)
            .Concat(domain.Publishers.SelectMany(item => item.Events).Select(item => item.Node))
            .Concat(model.Connections.Select(item => item.Node with { Name = "RefundBet" }))
            .Concat(model.Models.Where(item => locations[item.Node.Id].File == "game.reference.md").Select(item => item.Node));
        foreach (var node in nodes)
        {
            var location = locations[node.Id];
            Assert.Contains($"- [{node.Name}]({location.File}#{location.Anchor}) — {node.Summary}\n", overview);
        }
        var sections = new[] { "## Operations", "## Events", "## Connections", "## Models" };
        Assert.Equal(sections, sections.OrderBy(section => overview.IndexOf(section, StringComparison.Ordinal)));
        foreach (var detail in new[] { "**Parameters", "**Returns", "**Payload", "**Remarks", "**Requirements", "[Source]", "| Name | Type", "<a id=" })
        {
            Assert.DoesNotContain(detail, overview);
        }
        Assert.Contains("[Game](game.md) — " + domain.Node.Summary, documents["index.md"]);
        Assert.Contains("[Overview](game.md)", documents["game.reference.md"]);
    }

    [Fact]
    public void Missing_summaries_leave_only_linked_names_in_overview_lists_and_the_index()
    {
        var model = BusinessApiReader.Read(null, typeof(BusinessModelTests).Assembly);
        var documents = BusinessApiMarkdown.Render(model).ToDictionary(item => item.RelativePath, item => item.Markdown);
        Assert.Contains("- [Gateway](gateway.md)\n", documents["index.md"]);
        var overview = documents["gateway.md"];
        Assert.Contains("- [Find](gateway.reference.md#type-controlbatyr-tests-gateway-findasync) — Looks up a reading.\n", overview);
        Assert.Contains("- [Save](gateway.reference.md#type-controlbatyr-tests-gateway-saveasync)\n", overview);
        Assert.Contains("- [ReadingState](gateway.reference.md#type-controlbatyr-tests-readingstate)\n", overview);
        Assert.DoesNotContain("|", overview);
        var overloads = model.Domains.Single(item => item.Name == "Overloads").Operations;
        var locations = MarkdownRenderer.Locations(model);
        foreach (var operation in overloads)
        {
            Assert.Contains($"- [Find](overloads.reference.md#{locations[operation.Node.Id].Anchor})\n", documents["overloads.md"]);
        }
    }

    [Fact]
    public void Generated_model_round_trips_and_renders_the_committed_Markdown()
    {
        var directory = SampleDirectory();
        var json = File.ReadAllText(Path.Combine(directory, "business-api.json"));
        Assert.DoesNotContain("\r", json);
        var model = JsonSerializer.Deserialize<BusinessApiModel>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;
        var restored = JsonSerializer.Deserialize<BusinessApiModel>(JsonSerializer.Serialize(model))!;
        Assert.Equal(JsonSerializer.Serialize(ReadSample()), JsonSerializer.Serialize(restored));
        Assert.Contains("business-api.json", File.ReadAllLines(Path.Combine(directory, ".controlbatyr-generated")));
        foreach (var document in Outputs.Select("markdown;mermaid").SelectMany(output => output.Render(restored)))
        {
            Assert.Equal(File.ReadAllText(Path.Combine(directory, document.RelativePath)).ReplaceLineEndings("\n"), document.Content);
        }
        Assert.DoesNotContain(directory, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Path.GetFullPath(Path.Combine(directory, "../../..")), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\\\\", json);
    }

    [Fact]
    public void Generated_json_uses_readable_camel_case_properties_and_omits_nulls()
    {
        var json = File.ReadAllText(Path.Combine(SampleDirectory(), "business-api.json"));
        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty("locations", out _));
        Assert.Contains("players' funds", json);
        Assert.Contains("\"arguments\": []", json);
        Assert.Contains("\"remarks\": \"\"", json);
        Check(document.RootElement);

        static void Check(JsonElement element)
        {
            Assert.NotEqual(JsonValueKind.Null, element.ValueKind);
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    Assert.Equal(JsonNamingPolicy.CamelCase.ConvertName(property.Name), property.Name);
                    Check(property.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    Check(item);
                }
            }
        }
    }

    [Fact]
    public void Renderer_maps_every_identifier_to_an_existing_readable_anchor_and_assembly_order_is_irrelevant()
    {
        var model = ReadSample();
        var documents = BusinessApiMarkdown.Render(model).ToDictionary(document => document.RelativePath, document => document.Markdown);
        var locations = MarkdownRenderer.Locations(model).Values;
        var operations = model.Domains.SelectMany(domain => domain.Operations).Concat(model.Models.SelectMany(item => item.Methods));
        var publishers = model.Domains.SelectMany(domain => domain.Publishers);
        var nodes = model.Domains.Select(domain => domain.Node).Concat(model.Models.Select(item => item.Node))
            .Concat(operations.Select(operation => operation.Node))
            .Concat(operations.SelectMany(operation => operation.Requirements).Select(requirement => requirement.Node))
            .Concat(publishers.Select(publisher => publisher.Node))
            .Concat(publishers.SelectMany(publisher => publisher.Events).Select(item => item.Node))
            .Concat(model.Models.SelectMany(item => item.Properties).Select(property => property.Node))
            .Concat(model.Models.SelectMany(item => item.Values).Select(value => value.Node))
            .Concat(model.Connections.Select(connection => connection.Node));
        Assert.Equal(nodes.Select(node => node.Id).Distinct().Order(), locations.Select(location => location.Id).Order());
        foreach (var location in locations)
        {
            Assert.Contains($"<a id=\"{location.Anchor}\"></a>", documents[location.File]);
        }
        var assemblies = ProductTypes.Find(typeof(Game.Tests.GameRequirements).Assembly).Select(type => type.Assembly).Distinct().ToArray();
        var reordered = BusinessApiReader.Read(SampleDirectory(), [.. assemblies.Reverse(), .. assemblies]);
        Assert.Equal(JsonSerializer.Serialize(model), JsonSerializer.Serialize(reordered));
    }

    [Fact]
    public void Renderer_accepts_a_hand_built_model_with_links_comments_and_requirements()
    {
        var manager = new BusinessNode("A:Fiction|T:Tickets", "Tickets", "Offers tickets.", "", "../Tickets.cs");
        var issue = new BusinessNode("A:Fiction|M:Tickets.Issue(System.Int32)", "Issue", "Issues a ticket.", "Once per request.", null);
        var ticket = new BusinessNode("A:Fiction|T:Ticket", "Ticket", "An issued ticket.", "", null);
        var requirement = new BusinessRequirement(new("A:Fiction.Tests|M:Checks.Keeps_identifier", "Keeps_identifier", "", "", "../Checks.cs"));
        var operation = new BusinessOperation(issue, [new("id", new("int", null, [], ""), "Requested identifier.")],
            new("Ticket", ticket.Id, [], "?"), "The issued ticket.", [requirement], []);
        var model = new BusinessApiModel([new("Tickets", manager, [operation], [], [])],
            [new(ticket, false, [], [], [])], []);

        var documents = BusinessApiMarkdown.Render(model).ToDictionary(document => document.RelativePath, document => document.Markdown);

        Assert.Contains("[Source](../Tickets.cs)", documents["tickets.reference.md"]);
        Assert.Contains("| id | int | Requested identifier. |", documents["tickets.reference.md"]);
        Assert.Contains("**Returns:** [Ticket](#type-ticket)?", documents["tickets.reference.md"]);
        Assert.Contains("**Remarks**\n\nOnce per request.", documents["tickets.reference.md"]);
        Assert.Contains("- Keeps identifier — [Source](../Checks.cs)", documents["tickets.reference.md"]);
        Assert.Contains("An issued ticket.", documents["tickets.reference.md"]);
    }

    [Fact]
    public void Overloads_keep_distinct_xml_identifiers_and_distinct_readable_anchors()
    {
        var model = BusinessApiReader.Read(null, typeof(BusinessModelTests).Assembly);
        var operations = model.Domains.Single(domain => domain.Name == "Overloads").Operations;
        Assert.Contains(operations, operation => operation.Node.Id.EndsWith("|M:ControlBatyr.Tests.OverloadsManager.Find(System.Int32)", StringComparison.Ordinal));
        Assert.Contains(operations, operation => operation.Node.Id.EndsWith("|M:ControlBatyr.Tests.OverloadsManager.Find(System.String)", StringComparison.Ordinal));
        var locations = MarkdownRenderer.Locations(model);
        var anchors = operations.Select(operation => locations[operation.Node.Id].Anchor).ToArray();
        Assert.Equal(2, anchors.Distinct().Count());
        Assert.All(anchors, anchor => Assert.StartsWith("type-controlbatyr-tests-overloadsmanager-find-", anchor));
    }
}

[Manager]
public sealed class OverloadsManager
{
    public int Find(int id)
    {
        return id;
    }
    public string Find(string name)
    {
        return name;
    }
}
