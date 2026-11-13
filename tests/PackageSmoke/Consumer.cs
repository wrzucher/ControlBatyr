using ControlBatyr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ControlBatyr.Requirements;
using Markdig;
using PackageSmoke.Catalog;

namespace PackageSmoke;

public static class SmokeScenario
{
    public static void Run()
    {
        TicketRequirements.Issued_ticket_keeps_its_identifier();
        using var provider = new ServiceCollection().AddLogging().AddSingleton<TicketPublisher>()
            .AddSingleton(new MarkdownPipelineBuilder().Build()).AddSingleton<TicketCatalog>()
            .AddScoped<TicketManager>().BuildServiceProvider();
        provider.ConnectDomains(typeof(TicketManager).Assembly);
        provider.GetRequiredService<TicketPublisher>().NotifyIssued(new Ticket(42));
        if (!TicketConnections.Delivered)
        {
            throw new InvalidOperationException("The packaged connection must deliver the ticket.");
        }
    }
}

/// <summary>Offers tickets for a fictional event.</summary>
[Manager]
internal sealed class TicketManager(TicketPublisher publisher, ILogger<TicketManager> logger,
    MarkdownPipeline pipeline, TicketCatalog catalog)
{
    private readonly TicketPublisher publisher = publisher;
    private readonly ILogger<TicketManager> logger = logger;
    /// <summary>Issues a ticket with the requested identifier.</summary>
    public Ticket Issue(int id)
    {
        _ = Markdown.ToHtml(catalog.Description, pipeline);
        logger.LogDebug("Issuing ticket {Id}.", id);
        var ticket = new Ticket(id);
        publisher.NotifyIssued(ticket);
        return ticket;
    }
}

/// <summary>A ticket issued for an event.</summary>
[ObjectModel]
public sealed record Ticket(int Id);

public static class TicketRequirements
{
    [Requirement<TicketManager>(nameof(TicketManager.Issue))]
    public static void Issued_ticket_keeps_its_identifier()
    {
        if (new TicketManager(new TicketPublisher(), NullLogger<TicketManager>.Instance,
            new MarkdownPipelineBuilder().Build(), new TicketCatalog()).Issue(42).Id != 42)
        {
            throw new InvalidOperationException("The ticket must keep its identifier.");
        }
    }
}


/// <summary>Announces issued tickets.</summary>
[EventPublisher]
public sealed class TicketPublisher
{
    /// <summary>A ticket has been issued.</summary>
    public event Action<Ticket>? Issued;
    internal void NotifyIssued(Ticket ticket) => Issued?.Invoke(ticket);
}

internal static class TicketConnections
{
    internal static bool Delivered { get; private set; }

    [On<TicketPublisher>(nameof(TicketPublisher.Issued))]
    internal static Task DeliverAsync(TicketManager manager, Ticket ticket)
    {
        Delivered = ticket.Id == 42;
        return Task.CompletedTask;
    }
}
