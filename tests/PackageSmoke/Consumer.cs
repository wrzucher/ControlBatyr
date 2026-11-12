using ControlBatyr;
using ControlBatyr.Requirements;

namespace PackageSmoke;

internal static class Program
{
    private static void Main() => TicketRequirements.Issued_ticket_keeps_its_identifier();
}

/// <summary>Offers tickets for a fictional event.</summary>
[Manager]
public sealed class TicketManager
{
    /// <summary>Issues a ticket with the requested identifier.</summary>
    public Ticket Issue(int id) => new(id);
}

/// <summary>A ticket issued for an event.</summary>
[ObjectModel]
public sealed record Ticket(int Id);

public static class TicketRequirements
{
    [Requirement<TicketManager>(nameof(TicketManager.Issue))]
    public static void Issued_ticket_keeps_its_identifier()
    {
        if (new TicketManager().Issue(42).Id != 42)
        {
            throw new InvalidOperationException("The ticket must keep its identifier.");
        }
    }
}
