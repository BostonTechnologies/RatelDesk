using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;

namespace Helpdesk.API.Documentation;

/// <summary>Single source of truth for the public API reference navigation.</summary>
public static class RatelDeskOpenApiCatalog
{
    private sealed record Tag(string Name, string Group, string Description);

    private static readonly Tag[] Tags =
    [
        new("Setup", "Getting Started", "Bootstrap and setup state."),
        new("Authentication", "Identity & Access", "Current identity and authentication integrations."),
        new("Local Authentication", "Identity & Access", "Browser cookie sign-in and MFA for local accounts."),
        new("Customer Authentication", "Identity & Access", "Customer identity linking."),
        new("Users", "Identity & Access", "Application users and access."),
        new("Role Definitions", "Identity & Access", "Scoped application role definitions."),
        new("Integration Credentials", "Identity & Access", "Revocable API credentials. Secrets are shown once."),
        new("Organizations", "Organizations & Customers", "Application tenant organizations."),
        new("Customers", "Organizations & Customers", "Contacts; a customer is not the application tenant."),
        new("Tenant Administration", "Organizations & Customers", "Tenant-scoped administration."),
        new("Tenant Settings", "Organizations & Customers", "Tenant configuration."),
        new("Tenant Branding", "Organizations & Customers", "Tenant branding."),
        new("Support Groups", "Organizations & Customers", "Support groups."),
        new("Support Group Members", "Organizations & Customers", "Support group membership."),
        new("Support Coverage", "Organizations & Customers", "Support coverage."),
        new("Tickets", "Ticketing", "Shared ticket operations."), new("Incidents", "Ticketing", "Incident tickets."), new("Requests", "Ticketing", "Service requests."), new("Request Tasks", "Ticketing", "Request tasks."), new("Request Approvals", "Ticketing", "Request approvals."), new("Changes", "Ticketing", "Change tickets."), new("Work Logs", "Ticketing", "Ticket work logs."), new("Attachments", "Ticketing", "Ticket attachments."), new("Timeline", "Ticketing", "Ticket timeline."),
        new("Services", "Service Catalog & Self-Service", "Service catalog."), new("Request Forms", "Service Catalog & Self-Service", "Request forms."), new("Ticket Categories", "Service Catalog & Self-Service", "Ticket categories."), new("Ticket Lookups", "Service Catalog & Self-Service", "Ticket lookup values."), new("Self-Service", "Service Catalog & Self-Service", "Customer self-service."), new("Public Tickets", "Service Catalog & Self-Service", "Public ticket submission."), new("CAPTCHA", "Service Catalog & Self-Service", "CAPTCHA verification."),
        new("SLA Policies", "Service Levels", "SLA policies."), new("SLA Calendars", "Service Levels", "Working calendars."), new("Tenant SLA Settings", "Service Levels", "Tenant SLA settings."), new("Ticket SLA", "Service Levels", "Ticket SLA state."), new("SLA Reports", "Service Levels", "SLA reporting."), new("SLA Report Subscriptions", "Service Levels", "SLA report subscriptions."),
        new("Email Settings", "Email & Notifications", "Email settings."), new("Email Processing", "Email & Notifications", "Email processing."), new("Inbound Email Rules", "Email & Notifications", "Inbound email rules."), new("Email Layouts", "Email & Notifications", "Email layouts."), new("Email Templates", "Email & Notifications", "Email templates."), new("Notifications", "Email & Notifications", "Notifications."), new("Support Notification Subscriptions", "Email & Notifications", "Support notification subscriptions."), new("Support Notification Preferences", "Email & Notifications", "Support notification preferences."),
        new("Automation Rules", "Automation & Integrations", "Automation rules."), new("Workflow Operations", "Automation & Integrations", "Workflow operations."), new("External Orchestration", "Automation & Integrations", "External orchestration."), new("Orchestration Provider", "Automation & Integrations", "Orchestration provider."),
        new("AI Assistant", "AI Assistant", "AI assistant operations."), new("AI Assistant Chat", "AI Assistant", "AI assistant chat."), new("AI Assistant Webhooks", "AI Assistant", "AI assistant webhooks."), new("AI Assistant MCP", "AI Assistant", "Application MCP callbacks; distinct from the public MCP host."),
        new("Dashboard", "Reporting & Search", "Dashboards."), new("Global Search", "Reporting & Search", "Global search."), new("Assets", "Assets & Data", "Assets."), new("Resources", "Assets & Data", "Resource datasets."),
        new("Instance Branding", "System & Diagnostics", "Instance-wide branding."), new("System", "System & Diagnostics", "System operations."), new("System Tickets", "System & Diagnostics", "Machine/system-only ticket contract."), new("Presence", "System & Diagnostics", "User presence."), new("Background Jobs", "System & Diagnostics", "Background job operations."), new("AI Agent Operations", "System & Diagnostics", "AI agent diagnostics."), new("Health", "System & Diagnostics", "Health checks.")
    ];

    private static readonly IReadOnlyDictionary<string, string> CanonicalNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Local authentication"] = "Local Authentication", ["Role definitions"] = "Role Definitions", ["Tenant administration"] = "Tenant Administration", ["Tenant settings"] = "Tenant Settings", ["Ticketing"] = "Ticket Lookups", ["Captcha"] = "CAPTCHA", ["SLA"] = "SLA Policies", ["Email"] = "Email Processing", ["Workflow Ops"] = "Workflow Operations", ["AutomationRules"] = "Automation Rules", ["Branding"] = "Instance Branding", ["AI Agent Ops"] = "AI Agent Operations", ["Ops"] = "Background Jobs", ["Admin"] = "Tenant Administration", ["AiAssistant Chat"] = "AI Assistant Chat", ["AiAssistant Webhooks"] = "AI Assistant Webhooks", ["External orchestration"] = "External Orchestration", ["Orchestration provider"] = "Orchestration Provider", ["Self Service"] = "Self-Service"
    };

    public static Task TransformDocumentAsync(OpenApiDocument document, CancellationToken cancellationToken)
    {
        document.Info.Title = "RatelDesk API";
        document.Info.Description = "Use the Web-hosted reference at `/api/docs`. The API base URL is the server selected in Scalar. Local sign-in uses a browser cookie and CSRF protection; CLI and MCP use configured integration credentials. Organizations are application tenants; Customers are contacts. Pagination, filters, and errors are documented per operation.";
        document.Tags = Tags.Select(tag => new OpenApiTag { Name = tag.Name, Description = tag.Description }).ToHashSet();
        document.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        document.Extensions["x-tagGroups"] = new JsonNodeExtension(new JsonArray(Tags.GroupBy(tag => tag.Group).Select(group => (JsonNode)new JsonObject
        {
            ["name"] = group.Key,
            ["tags"] = new JsonArray(group.Select(tag => (JsonNode)tag.Name).ToArray())
        }).ToArray()));
        return Task.CompletedTask;
    }

    public static Task TransformOperationAsync(OpenApiOperation operation, ApiDescription description, CancellationToken cancellationToken)
    {
        var tag = operation.Tags?.Select(item => item.Name).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(tag) && CanonicalNames.TryGetValue(tag, out var canonical))
        {
            operation.Tags!.Clear();
            operation.Tags.Add(new OpenApiTagReference(canonical));
        }

        var metadata = description.ActionDescriptor.EndpointMetadata;
        if (metadata?.OfType<IAllowAnonymous>().Any() == true)
            operation.Security = [];
        return Task.CompletedTask;
    }
}
