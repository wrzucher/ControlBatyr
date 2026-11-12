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
    public void Generated_model_round_trips_and_renders_the_committed_Markdown()
    {
        var directory = SampleDirectory();
        var json = File.ReadAllText(Path.Combine(directory, "business-api.json"));
        Assert.DoesNotContain("\r", json);
        var model = JsonSerializer.Deserialize<BusinessApiModel>(json)!;
        var restored = JsonSerializer.Deserialize<BusinessApiModel>(JsonSerializer.Serialize(model))!;
        Assert.Equal(JsonSerializer.Serialize(ReadSample()), JsonSerializer.Serialize(restored));
        Assert.Contains("business-api.json", File.ReadAllLines(Path.Combine(directory, ".controlbatyr-generated")));
        foreach (var document in BusinessApiMarkdown.Render(restored))
        {
            Assert.Equal(File.ReadAllText(Path.Combine(directory, document.RelativePath)).ReplaceLineEndings("\n"), document.Markdown);
        }
        Assert.DoesNotContain(Path.GetPathRoot(directory)!, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\\\\", json);
    }

    [Fact]
    public void Every_identifier_maps_to_an_existing_readable_anchor_and_assembly_order_is_irrelevant()
    {
        var model = ReadSample();
        var documents = BusinessApiMarkdown.Render(model).ToDictionary(document => document.RelativePath, document => document.Markdown);
        Assert.Equal(model.Locations.Length, model.Locations.Select(location => location.Id).Distinct().Count());
        foreach (var location in model.Locations)
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
            new("Ticket", ticket.Id, [], "?"), "The issued ticket.", [requirement]);
        var model = new BusinessApiModel([new("Tickets", manager, [operation], [])],
            [new(ticket, false, [], [], [])], [],
            [new(manager.Id, "tickets.md", "tickets"), new(issue.Id, "tickets.md", "issue"), new(ticket.Id, "shared.md", "ticket")]);

        var documents = BusinessApiMarkdown.Render(model).ToDictionary(document => document.RelativePath, document => document.Markdown);

        Assert.Contains("[Source](../Tickets.cs)", documents["tickets.md"]);
        Assert.Contains("| id | int | Requested identifier. |", documents["tickets.md"]);
        Assert.Contains("**Returns:** [Ticket](shared.md#ticket)?", documents["tickets.md"]);
        Assert.Contains("**Remarks**\n\nOnce per request.", documents["tickets.md"]);
        Assert.Contains("- Keeps identifier — [Source](../Checks.cs)", documents["tickets.md"]);
        Assert.Contains("An issued ticket.", documents["shared.md"]);
    }

    [Fact]
    public void Overloads_keep_distinct_xml_identifiers_and_distinct_readable_anchors()
    {
        var model = BusinessApiReader.Read(null, typeof(BusinessModelTests).Assembly);
        var operations = model.Domains.Single(domain => domain.Name == "Overloads").Operations;
        Assert.Contains(operations, operation => operation.Node.Id.EndsWith("|M:ControlBatyr.Tests.OverloadsManager.Find(System.Int32)", StringComparison.Ordinal));
        Assert.Contains(operations, operation => operation.Node.Id.EndsWith("|M:ControlBatyr.Tests.OverloadsManager.Find(System.String)", StringComparison.Ordinal));
        var anchors = operations.Select(operation => model.Locations.Single(location => location.Id == operation.Node.Id).Anchor).ToArray();
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
