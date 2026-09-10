using Dodo.Primitives;
using System.ComponentModel.DataAnnotations.Schema;

namespace Helpdesk.Shared.Models;

/// <summary>
/// Represents effort logged against a ticket.
/// </summary>
public class WorkLog
{
    /// <summary>
    /// Identifier for the work log entry.
    /// </summary>
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();

    /// <summary>
    /// The ticket this log entry relates to.
    /// </summary>
    public string TicketId { get; set; } = string.Empty;

    /// <summary>
    /// Amount of time spent on the work in hours.
    /// </summary>
    public double Hours { get; set; }

    /// <summary>
    /// Internal notes stay in the staff timeline and do not send customer/listener email.
    /// </summary>
    public bool IsInternalNote { get; set; }

    /// <summary>
    /// Optional rich-text notes (sanitized HTML) describing the work performed.
    /// </summary>
    public string? NotesHtml { get; set; }

    /// <summary>
    /// Optional plain-text representation of the notes for previews/search.
    /// </summary>
    public string? NotesText { get; set; }

    [NotMapped]
    public string? Notes
    {
        get => NotesText;
        set => NotesText = value;
    }

    /// <summary>
    /// When the log entry was created.
    /// </summary>
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Identifier of the technician who performed the work.
    /// </summary>
    public string? TechnicianId { get; set; }

    /// <summary>
    /// When the customer viewed this work log.
    /// </summary>
    public DateTime? SeenByCustomerAt { get; set; }
}
