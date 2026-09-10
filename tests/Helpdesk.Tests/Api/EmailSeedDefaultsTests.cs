using Helpdesk.API.Email;
using Helpdesk.Application.Services.EmailTemplates;
using Helpdesk.Infrastructure.EmailTemplates;

namespace Helpdesk.Tests.Api;

public class EmailSeedDefaultsTests
{
    [Fact]
    public void DefaultLayout_IncludesBrandingTokens_AndBodySlot()
    {
        var layout = EmailSeedDefaults.DefaultLayoutHtml;

        Assert.Contains("{{{BODY}}}", layout);
        Assert.Contains("{{{LOGO_HTML}}}", layout);
        Assert.Contains("{{{FOOTER_HTML}}}", layout);
        Assert.Contains("{{PRIMARY_COLOR}}", layout);
        Assert.Contains("max-width:640px", layout);
        Assert.Contains("background:#f8fbff", layout);
        Assert.Contains("padding:16px 24px", layout);
        Assert.DoesNotContain("background:#081120", layout);
    }

    [Fact]
    public void TicketUpdatedTemplate_RendersHelpdeskBubble_AndPreservesUpdateHtml()
    {
        var template = File.ReadAllText(FindRepoFile("src/Helpdesk.API/wwwroot/email-templates/TicketUpdated.html"));
        var renderer = new EmailTemplateRenderer(new TemplateEngine());

        var rendered = renderer.Render(template, new EmailTemplateContext
        {
            UserName = "Jane",
            TicketRef = "INC-123",
            TicketLink = "https://helpdesk.example/tickets/INC-123",
            UpdateMessageHtml = "<p><strong>Router reboot scheduled.</strong></p>",
            PrimaryColor = "#0ea5e9"
        });

        Assert.Contains("Update from Helpdesk", rendered);
        Assert.Contains("Reply to this email", rendered);
        Assert.Contains("#0ea5e9", rendered);
        Assert.Contains("<p><strong>Router reboot scheduled.</strong></p>", rendered);
    }

    [Fact]
    public void NewTicketConfirmationTemplate_RendersTicketType_TrackingRef_AndLink()
    {
        var template = File.ReadAllText(FindRepoFile("src/Helpdesk.API/wwwroot/email-templates/NewTicketConfirmation.html"));
        var renderer = new EmailTemplateRenderer(new TemplateEngine());

        var rendered = renderer.Render(template, new EmailTemplateContext
        {
            UserName = "Jane",
            TicketRef = "REQ-123",
            TicketType = "Request",
            TicketTypeLower = "request",
            TicketLink = "https://helpdesk.example/view-ticket/REQ-123",
            PrimaryColor = "#0ea5e9"
        });

        Assert.Contains("Your Request has been created", rendered);
        Assert.Contains("REQ-123", rendered);
        Assert.Contains("https://helpdesk.example/view-ticket/REQ-123", rendered);
        Assert.Contains("View Ticket", rendered);
    }

    [Theory]
    [InlineData("ChangeSubmitted")]
    [InlineData("ChangeReminder")]
    [InlineData("ChangeApprovalRequired")]
    [InlineData("ChangeApproved")]
    [InlineData("ChangeRejected")]
    [InlineData("ChangeImplementationInProgress")]
    [InlineData("ChangeImplemented")]
    [InlineData("ChangeUpdate")]
    [InlineData("RequestApprovalRequired")]
    public void ChangeTemplates_AreSeededWithSubjects(string templateName)
    {
        Assert.Contains(templateName, EmailSeedDefaults.TemplateNames);
        Assert.NotEqual("Helpdesk Notification", EmailSeedDefaults.GenerateSubjectFromName(templateName));
    }

    [Fact]
    public void ChangeSubmittedTemplate_RendersChangeDetails()
    {
        var template = File.ReadAllText(FindRepoFile("src/Helpdesk.API/wwwroot/email-templates/ChangeSubmitted.html"));
        var renderer = new EmailTemplateRenderer(new TemplateEngine());

        var rendered = renderer.Render(template, new EmailTemplateContext
        {
            UserName = "Jane",
            TicketRef = "CHG-123",
            ChangeTitle = "Firewall update",
            ChangeType = "Normal",
            ChangePriority = "High",
            ChangeOrganization = "Example Organization",
            ChangeRequestedFor = "Alice",
            ChangeImplementor = "Bob",
            ChangeImplementationStart = "2026/05/22 20:00",
            ChangeImplementationEnd = "2026/05/22 22:00",
            ChangeViewLink = "https://helpdesk.example/change-approval/CHG-123?email=alice@example.com&token=signed",
            ChangeScopeOfChange = "Patch firewalls",
            ChangeAffectedSystems = "<ul><li>Firewall pair</li></ul>",
            ChangeImplementationSteps = "<ul><li>Apply firmware</li></ul>",
            ChangeValidationSteps = "<ul><li>Confirm VPN</li></ul>",
            ChangeRollbackPlan = "Restore previous image",
            PrimaryColor = "#0ea5e9"
        });

        Assert.Contains("CHG-123", rendered);
        Assert.Contains("Firewall update", rendered);
        Assert.Contains("Normal", rendered);
        Assert.Contains("Example Organization", rendered);
        Assert.Contains("2026/05/22 20:00", rendered);
        Assert.Contains("View Change", rendered);
        Assert.Contains("https://helpdesk.example/change-approval/CHG-123", rendered);
        Assert.Contains("Patch firewalls", rendered);
        Assert.Contains("Firewall pair", rendered);
        Assert.Contains("Apply firmware", rendered);
        Assert.Contains("Confirm VPN", rendered);
        Assert.Contains("Restore previous image", rendered);
        Assert.Contains("color:#111827 !important", rendered);
    }

    [Fact]
    public void ChangeApprovalRequiredTemplate_RendersProtectedApprovalLink()
    {
        var template = File.ReadAllText(FindRepoFile("src/Helpdesk.API/wwwroot/email-templates/ChangeApprovalRequired.html"));
        var renderer = new EmailTemplateRenderer(new TemplateEngine());

        var rendered = renderer.Render(template, new EmailTemplateContext
        {
            UserName = "Jane",
            TicketRef = "CHG-123",
            ChangeTitle = "Firewall update",
            ChangeType = "Normal",
            ChangeApprovers = "Jane Approver",
            ChangeImplementationStart = "2026/05/22 20:00",
            ChangeImplementationEnd = "2026/05/22 22:00",
            ChangeApprovalLink = "https://helpdesk.example/change-approval/CHG-123?email=jane@example.com&token=signed",
            ChangeScopeOfChange = "Patch firewalls",
            ChangeAffectedSystems = "<ul><li>Firewall pair</li></ul>",
            ChangeImplementationSteps = "<ul><li>Apply firmware</li></ul>",
            ChangeValidationSteps = "<ul><li>Confirm VPN</li></ul>",
            ChangeRollbackPlan = "Restore previous image",
            PrimaryColor = "#0ea5e9"
        });

        Assert.Contains("Review / Approve Change", rendered);
        Assert.Contains("https://helpdesk.example/change-approval/CHG-123", rendered);
        Assert.Contains("signed", rendered);
        Assert.Contains("Patch firewalls", rendered);
        Assert.Contains("Firewall pair", rendered);
        Assert.Contains("color:#111827 !important", rendered);
    }

    [Fact]
    public void CustomerInvitationTemplate_IsSeededWithSubject()
    {
        Assert.Contains("CustomerInvitation", EmailSeedDefaults.TemplateNames);
        Assert.Equal(
            "Your RatelDesk invitation",
            EmailSeedDefaults.GenerateSubjectFromName("CustomerInvitation"));
    }

    [Fact]
    public void CustomerInvitationTemplate_RendersBrandedInviteDetails()
    {
        var template = File.ReadAllText(FindRepoFile("src/Helpdesk.API/wwwroot/email-templates/CustomerInvitation.html"));
        var renderer = new EmailTemplateRenderer(new TemplateEngine());

        var rendered = renderer.Render(template, new EmailTemplateContext
        {
            UserName = "Example User",
            OrganizationName = "Example Organization",
            InviteLink = "https://auth.example/recovery?token=signed",
            InviteExpiresAt = "2026-06-01 14:15 UTC",
            HelpdeskUrl = "https://helpdesk.example.com",
            PrimaryColor = "#0ea5e9"
        });

        Assert.Contains("Example User", rendered);
        Assert.Contains("Example Organization", rendered);
        Assert.Contains("Activate Helpdesk Access", rendered);
        Assert.Contains("https://auth.example/recovery?token=signed", rendered);
        Assert.Contains("https://helpdesk.example.com", rendered);
        Assert.Contains("2026-06-01 14:15 UTC", rendered);
        Assert.Contains("background:#f59e0b", rendered);
        Assert.Contains("border:1px solid #d97706", rendered);
        Assert.Contains("color:#111827 !important", rendered);
    }

    [Fact]
    public void RequestApprovalRequiredTemplate_RendersApprovalDetailsAndLinks()
    {
        var template = File.ReadAllText(FindRepoFile("src/Helpdesk.API/wwwroot/email-templates/RequestApprovalRequired.html"));
        var renderer = new EmailTemplateRenderer(new TemplateEngine());

        var rendered = renderer.Render(template, new EmailTemplateContext
        {
            UserName = "Jane",
            TicketRef = "REQ-123",
            RequestTitle = "New laptop",
            RequestDescription = "Developer workstation",
            RequestRequestedBy = "alice@example.com",
            RequestRequestedFor = "Alice",
            RequestApprovers = "Jane Approver",
            RequestPayloadHtml = "<table><tr><td>Device</td><td>Laptop</td></tr></table>",
            RequestApprovalDueAt = "2026-06-10 08:00 UTC",
            RequestApprovalLink = "https://helpdesk.example/request-approval/REQ-123/task-1?email=jane@example.com&token=signed",
            RequestRejectLink = "https://helpdesk.example/request-approval/REQ-123/task-1?email=jane@example.com&token=signed&action=reject",
            PrimaryColor = "#0ea5e9"
        });

        Assert.Contains("Review / Approve Request", rendered);
        Assert.Contains("Reject Request", rendered);
        Assert.Contains("New laptop", rendered);
        Assert.Contains("Developer workstation", rendered);
        Assert.Contains("alice@example.com", rendered);
        Assert.Contains("Device", rendered);
        Assert.Contains("https://helpdesk.example/request-approval/REQ-123/task-1", rendered);
        Assert.Contains("color:#111827 !important", rendered);
    }

    [Theory]
    [InlineData("NewTicketConfirmation.html", "View Ticket")]
    [InlineData("TicketUpdated.html", "View Ticket Update")]
    [InlineData("IncidentResolved.html", "View Incident")]
    [InlineData("RequestResolved.html", "View Request")]
    [InlineData("SelfServiceRequestCreated.html", "View Request")]
    [InlineData("SelfServiceRequestCompleted.html", "View Request")]
    [InlineData("SelfServiceRequestFailed.html", "View Investigation Incident")]
    [InlineData("RequestApprovalRequired.html", "Review / Approve Request")]
    [InlineData("ChangeImplementationInProgress.html", "View Change")]
    [InlineData("ChangeImplemented.html", "View Change")]
    [InlineData("ChangeUpdate.html", "View Change")]
    public void LinkTemplates_RenderVisibleCtaText(string fileName, string buttonText)
    {
        var template = File.ReadAllText(FindRepoFile($"src/Helpdesk.API/wwwroot/email-templates/{fileName}"));
        var renderer = new EmailTemplateRenderer(new TemplateEngine());

        var rendered = renderer.Render(template, new EmailTemplateContext
        {
            UserName = "Jane",
            TicketRef = "REQ-123",
            TicketType = "Request",
            TicketLink = "https://helpdesk.example/view-ticket/REQ-123",
            IncidentRef = "INC-456",
            IncidentLink = "https://helpdesk.example/view-ticket/INC-456",
            ServiceName = "Disk report",
            RequestTitle = "Disk report",
            FailureReason = "Automation failed",
            RequestApprovalLink = "https://helpdesk.example/request-approval/REQ-123/task-1?email=jane@example.com&token=signed",
            RequestRejectLink = "https://helpdesk.example/request-approval/REQ-123/task-1?email=jane@example.com&token=signed&action=reject",
            ChangeTitle = "Firewall update",
            ChangeViewLink = "https://helpdesk.example/change-approval/CHG-123?email=jane@example.com&token=signed",
            ChangeCompletionState = "Success",
            PrimaryColor = "#0ea5e9"
        });

        Assert.Contains(buttonText, rendered);
        Assert.Contains("color:#111827 !important", rendered);
        Assert.Contains("background:#f59e0b", rendered);
        Assert.Contains("border:1px solid #d97706", rendered);
        Assert.DoesNotContain("background:#0ea5e9; border:1px solid #0ea5e9; color:#ffffff !important", rendered);
    }

    [Theory]
    [InlineData(true, 1, "<p>Custom system template</p>", true)]
    [InlineData(true, 2, "<p>Custom system template</p>", true)]
    [InlineData(true, 3, "<p>Custom system template</p>", true)]
    [InlineData(true, 4, "<p>Custom system template</p>", true)]
    [InlineData(true, 5, "<p>Custom system template</p>", true)]
    [InlineData(true, 6, "<p>Custom system template</p>", true)]
    [InlineData(true, 7, "<p>Custom system template</p>", true)]
    [InlineData(true, 8, "<p>Custom system template</p>", true)]
    [InlineData(true, 9, "<p>Custom system template</p>", true)]
    [InlineData(true, 10, "<p>Custom system template</p>", false)]
    [InlineData(false, 1, "<p>Custom tenant template</p>", false)]
    [InlineData(false, 2, "<p><em>(Your Company Logo Here)</em></p>", true)]
    [InlineData(false, 5, "<p>Thank you for contacting our helpdesk. Your ticket has been successfully created.</p>", true)]
    public void ShouldUpgradeDefaultTemplate_UpgradesOldSystemRows_AndLegacyOrchestrationckHtml(
        bool isSystem,
        int version,
        string html,
        bool expected)
    {
        Assert.Equal(expected, EmailSeedDefaults.ShouldUpgradeDefaultTemplate(isSystem, version, html));
    }

    [Theory]
    [InlineData(true, 1, "<div>Custom system layout</div>", true)]
    [InlineData(true, 2, "<div>Custom system layout</div>", true)]
    [InlineData(true, 3, "<div>Custom system layout</div>", true)]
    [InlineData(true, 4, "<div>Custom system layout</div>", true)]
    [InlineData(true, 5, "<div>Custom system layout</div>", true)]
    [InlineData(true, 6, "<div>Custom system layout</div>", true)]
    [InlineData(true, 7, "<div>Custom system layout</div>", true)]
    [InlineData(true, 8, "<div>Custom system layout</div>", true)]
    [InlineData(true, 9, "<div>Custom system layout</div>", true)]
    [InlineData(true, 10, "<div>Custom system layout</div>", false)]
    [InlineData(false, 1, "<div>Custom tenant layout</div>", false)]
    [InlineData(false, 3, "<div style=\"font-family: Arial, sans-serif; color: #222;\">{{{BODY}}}</div>", true)]
    public void ShouldUpgradeDefaultLayout_UpgradesOldSystemRows_AndLegacyOrchestrationckHtml(
        bool isSystem,
        int version,
        string html,
        bool expected)
    {
        Assert.Equal(expected, EmailSeedDefaults.ShouldUpgradeDefaultLayout(isSystem, version, html));
    }

    private static string FindRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find {relativePath} from {AppContext.BaseDirectory}.");
    }
}
