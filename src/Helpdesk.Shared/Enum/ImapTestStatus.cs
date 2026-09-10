using System.ComponentModel.DataAnnotations;

namespace Helpdesk.Shared.Enums;

public enum ImapTestStatus
{
    [Display(Name = "Never Tested")]
    Never,

    [Display(Name = "Test Passed")]
    Success,

    [Display(Name = "Test Failed")]
    Failed
}

