using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Helpdesk.Infrastructure.Persistence.SeedData;

public static class TicketCategorySeed
{
    private sealed record SeedNode(string Key, string Name, string? Description, string? ParentKey, int SortOrder, TicketCategoryType Type);

    public static async Task SeedAsync(HelpdeskDbContext db, CancellationToken cancellationToken = default)
    {
        var nodes = BuildTaxonomy();
        var idByKey = nodes.ToDictionary(x => x.Key, x => DeterministicGuid($"ticket-category:{x.Key}"));

        foreach (var node in nodes)
        {
            if (await db.TicketCategories.AnyAsync(x =>
                    x.Name == node.Name &&
                    x.Type == node.Type &&
                    x.TenantId == null,
                cancellationToken))
            {
                continue;
            }

            Guid? parentCategoryId = null;
            if (node.ParentKey is not null)
            {
                var candidateParentId = idByKey[node.ParentKey];
                var parentExists = await db.TicketCategories.AnyAsync(x => x.Id == candidateParentId, cancellationToken);
                parentCategoryId = parentExists ? candidateParentId : null;
            }

            db.TicketCategories.Add(new TicketCategory
            {
                Id = idByKey[node.Key],
                Name = node.Name,
                Description = node.Description,
                Type = node.Type,
                TenantId = null,
                ParentCategoryId = parentCategoryId,
                SortOrder = node.SortOrder,
                IsActive = true,
                IsSystem = true,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = null
            });

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static List<SeedNode> BuildTaxonomy()
    {
        var nodes = new List<SeedNode>();

        void Add(TicketCategoryType type, string key, string name, string? parentKey, int sortOrder, string? description = null)
            => nodes.Add(new SeedNode(key, name, description, parentKey, sortOrder, type));

        BuildServiceTaxonomy(Add);
        BuildIncidentTaxonomy(Add);
        BuildRequestTaxonomy(Add);
        BuildChangeTaxonomy(Add);

        return nodes;
    }

    private static void BuildServiceTaxonomy(Action<TicketCategoryType, string, string, string?, int, string?> add)
    {
        add(TicketCategoryType.Service, "service.infrastructure", "Infrastructure", null, 10, null);
        add(TicketCategoryType.Service, "service.infrastructure.onprem", "On-Premise Infrastructure", "service.infrastructure", 10, null);
        add(TicketCategoryType.Service, "service.infrastructure.onprem.ad", "Active Directory (On-Prem)", "service.infrastructure.onprem", 10, null);
        add(TicketCategoryType.Service, "service.infrastructure.onprem.exchange", "Exchange Server", "service.infrastructure.onprem", 20, null);
        add(TicketCategoryType.Service, "service.infrastructure.onprem.files", "File Servers", "service.infrastructure.onprem", 30, null);
        add(TicketCategoryType.Service, "service.infrastructure.onprem.print", "Print Servers", "service.infrastructure.onprem", 40, null);
        add(TicketCategoryType.Service, "service.infrastructure.onprem.gpo", "Group Policy", "service.infrastructure.onprem", 50, null);

        add(TicketCategoryType.Service, "service.infrastructure.cloud", "Cloud Infrastructure", "service.infrastructure", 20, null);
        add(TicketCategoryType.Service, "service.infrastructure.cloud.azure-vm", "Azure Virtual Machines", "service.infrastructure.cloud", 10, null);
        add(TicketCategoryType.Service, "service.infrastructure.cloud.azure-net", "Azure Networking", "service.infrastructure.cloud", 20, null);
        add(TicketCategoryType.Service, "service.infrastructure.cloud.azure-storage", "Azure Storage", "service.infrastructure.cloud", 30, null);
        add(TicketCategoryType.Service, "service.infrastructure.cloud.azure-backup", "Azure Backup", "service.infrastructure.cloud", 40, null);
        add(TicketCategoryType.Service, "service.infrastructure.cloud.azure-entra", "Azure Entra ID (Cloud)", "service.infrastructure.cloud", 50, null);
        add(TicketCategoryType.Service, "service.infrastructure.cloud.aad-connect", "Azure AD Connect", "service.infrastructure.cloud", 60, null);

        add(TicketCategoryType.Service, "service.networking", "Networking", null, 20, null);
        add(TicketCategoryType.Service, "service.networking.firewalls", "Firewalls", "service.networking", 10, null);
        add(TicketCategoryType.Service, "service.networking.vpn", "VPN", "service.networking", 20, null);
        add(TicketCategoryType.Service, "service.networking.vlan", "VLAN", "service.networking", 30, null);
        add(TicketCategoryType.Service, "service.networking.switching", "Switching", "service.networking", 40, null);
        add(TicketCategoryType.Service, "service.networking.routing", "Routing", "service.networking", 50, null);
        add(TicketCategoryType.Service, "service.networking.wap", "Wireless Access Points", "service.networking", 60, null);

        add(TicketCategoryType.Service, "service.virtualization", "Virtualization", null, 30, null);
        add(TicketCategoryType.Service, "service.virtualization.vmware", "VMware", "service.virtualization", 10, null);
        add(TicketCategoryType.Service, "service.virtualization.hyperv", "Hyper-V", "service.virtualization", 20, null);
        add(TicketCategoryType.Service, "service.virtualization.proxmox", "Proxmox", "service.virtualization", 30, null);

        add(TicketCategoryType.Service, "service.platform", "Platform Services", null, 40, null);
        add(TicketCategoryType.Service, "service.platform.m365", "Microsoft 365", "service.platform", 10, null);
        add(TicketCategoryType.Service, "service.platform.m365.exchange-online", "Exchange Online (M365)", "service.platform.m365", 10, null);
        add(TicketCategoryType.Service, "service.platform.m365.sharepoint", "SharePoint Online", "service.platform.m365", 20, null);
        add(TicketCategoryType.Service, "service.platform.m365.teams", "Teams (M365)", "service.platform.m365", 30, null);
        add(TicketCategoryType.Service, "service.platform.m365.onedrive", "OneDrive", "service.platform.m365", 40, null);

        add(TicketCategoryType.Service, "service.identity", "Identity Services", null, 50, null);
        add(TicketCategoryType.Service, "service.identity.entra", "Azure Entra ID", "service.identity", 10, null);
        add(TicketCategoryType.Service, "service.identity.ad", "Active Directory", "service.identity", 20, null);
        add(TicketCategoryType.Service, "service.identity.federation", "Federation Services", "service.identity", 30, null);

        add(TicketCategoryType.Service, "service.email", "Email", null, 60, null);
        add(TicketCategoryType.Service, "service.email.exchange-online", "Exchange Online", "service.email", 10, null);
        add(TicketCategoryType.Service, "service.email.exchange-onprem", "Exchange On-Premise", "service.email", 20, null);
        add(TicketCategoryType.Service, "service.email.smtp-relay", "SMTP Relay", "service.email", 30, null);

        add(TicketCategoryType.Service, "service.enduser", "End User", null, 70, null);
        add(TicketCategoryType.Service, "service.enduser.os", "Operating Systems", "service.enduser", 10, null);
        add(TicketCategoryType.Service, "service.enduser.os.win10", "Windows 10", "service.enduser.os", 10, null);
        add(TicketCategoryType.Service, "service.enduser.os.win11", "Windows 11", "service.enduser.os", 20, null);
        add(TicketCategoryType.Service, "service.enduser.os.linux", "Linux", "service.enduser.os", 30, null);
        add(TicketCategoryType.Service, "service.enduser.os.macos", "macOS", "service.enduser.os", 40, null);

        add(TicketCategoryType.Service, "service.enduser.apps", "Applications", "service.enduser", 20, null);
        add(TicketCategoryType.Service, "service.enduser.apps.outlook", "Outlook", "service.enduser.apps", 10, null);
        add(TicketCategoryType.Service, "service.enduser.apps.office", "Office 365 Apps", "service.enduser.apps", 20, null);
        add(TicketCategoryType.Service, "service.enduser.apps.teams", "Teams (Client App)", "service.enduser.apps", 30, null);
        add(TicketCategoryType.Service, "service.enduser.apps.browsers", "Browsers", "service.enduser.apps", 40, null);

        add(TicketCategoryType.Service, "service.enduser.devices", "Devices", "service.enduser", 30, null);
        add(TicketCategoryType.Service, "service.enduser.devices.desktop", "Desktop", "service.enduser.devices", 10, null);
        add(TicketCategoryType.Service, "service.enduser.devices.laptop", "Laptop", "service.enduser.devices", 20, null);
        add(TicketCategoryType.Service, "service.enduser.devices.mobile", "Mobile Phone", "service.enduser.devices", 30, null);
        add(TicketCategoryType.Service, "service.enduser.devices.printer", "Printer", "service.enduser.devices", 40, null);
    }

    private static void BuildIncidentTaxonomy(Action<TicketCategoryType, string, string, string?, int, string?> add)
    {
        add(TicketCategoryType.Incident, "incident.availability", "Availability", null, 10, null);
        add(TicketCategoryType.Incident, "incident.availability.outage", "Outage", "incident.availability", 10, null);
        add(TicketCategoryType.Incident, "incident.availability.degradation", "Service Degradation", "incident.availability", 20, null);

        add(TicketCategoryType.Incident, "incident.performance", "Performance", null, 20, null);
        add(TicketCategoryType.Incident, "incident.performance.latency", "Latency", "incident.performance", 10, null);
        add(TicketCategoryType.Incident, "incident.performance.capacity", "Capacity", "incident.performance", 20, null);

        add(TicketCategoryType.Incident, "incident.security", "Security", null, 30, null);
        add(TicketCategoryType.Incident, "incident.security.malware", "Malware", "incident.security", 10, null);
        add(TicketCategoryType.Incident, "incident.security.phishing", "Phishing", "incident.security", 20, null);

        add(TicketCategoryType.Incident, "incident.access", "Access Issue", null, 40, null);
        add(TicketCategoryType.Incident, "incident.access.locked", "Account Locked", "incident.access", 10, null);
        add(TicketCategoryType.Incident, "incident.access.auth", "Authentication Failure", "incident.access", 20, null);
    }

    private static void BuildRequestTaxonomy(Action<TicketCategoryType, string, string, string?, int, string?> add)
    {
        add(TicketCategoryType.Request, "request.access", "Access Request", null, 10, null);
        add(TicketCategoryType.Request, "request.access.new", "New Access", "request.access", 10, null);
        add(TicketCategoryType.Request, "request.access.modify", "Modify Access", "request.access", 20, null);
        add(TicketCategoryType.Request, "request.access.remove", "Remove Access", "request.access", 30, null);

        add(TicketCategoryType.Request, "request.hardware", "Hardware Request", null, 20, null);
        add(TicketCategoryType.Request, "request.hardware.laptop", "Laptop Request", "request.hardware", 10, null);
        add(TicketCategoryType.Request, "request.hardware.peripheral", "Peripheral Request", "request.hardware", 20, null);

        add(TicketCategoryType.Request, "request.software", "Software Request", null, 30, null);
        add(TicketCategoryType.Request, "request.software.install", "Software Installation", "request.software", 10, null);
        add(TicketCategoryType.Request, "request.software.license", "License Request", "request.software", 20, null);

        add(TicketCategoryType.Request, "request.information", "Information Request", null, 40, null);
        add(TicketCategoryType.Request, "request.information.report", "Report Request", "request.information", 10, null);
        add(TicketCategoryType.Request, "request.information.audit", "Audit Evidence", "request.information", 20, null);
    }

    private static void BuildChangeTaxonomy(Action<TicketCategoryType, string, string, string?, int, string?> add)
    {
        add(TicketCategoryType.Change, "change.management", "Change Management", null, 10, null);
        add(TicketCategoryType.Change, "change.management.infrastructure", "Infrastructure Change", "change.management", 10, null);
        add(TicketCategoryType.Change, "change.management.application", "Application Change", "change.management", 20, null);
        add(TicketCategoryType.Change, "change.management.security", "Security Change", "change.management", 30, null);
        add(TicketCategoryType.Change, "change.management.network", "Network Change", "change.management", 40, null);
        add(TicketCategoryType.Change, "change.management.cloud", "Cloud Change", "change.management", 50, null);
        add(TicketCategoryType.Change, "change.management.user-access", "User Access Change", "change.management", 60, null);
        add(TicketCategoryType.Change, "change.management.configuration", "Configuration Change", "change.management", 70, null);
        add(TicketCategoryType.Change, "change.management.patch", "Patch Management", "change.management", 80, null);
    }

    private static Guid DeterministicGuid(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes[..16].CopyTo(guidBytes);

        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x40);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);

        return new Guid(guidBytes);
    }
}
