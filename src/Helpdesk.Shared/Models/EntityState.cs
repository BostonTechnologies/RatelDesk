using System.ComponentModel.DataAnnotations;

namespace Helpdesk.Shared.Models;

public enum EntityState
{
    [Display(Name = "Enabled", ShortName = "EN", Description = "Enabled and allowed to operate")]
    Enabled,

    [Display(Name = "Blocked", ShortName = "BL", Description = "Disabled by an administrator")]
    Blocked
}
