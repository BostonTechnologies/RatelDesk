using System.ComponentModel.DataAnnotations;

namespace Helpdesk.Shared.Models;

public enum TicketState
{
    [Display(Name = "New")]
    New,

    [Display(Name = "Waiting for Reply")]
    WaitingReply,

    [Display(Name = "Customer Replied")]
    Replied,

    [Display(Name = "In Progress")]
    InProgress,

    [Display(Name = "Pending Approval")]
    PendingApproval,

    [Display(Name = "On Hold")]
    OnHold,

    [Display(Name = "Resolved")]
    Resolved
}
