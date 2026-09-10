using System.Net;
using System.Net.Http.Json;
using Helpdesk.Shared.DTOs.SupportNotifications;
using Helpdesk.Shared.Enums;

namespace HelpDesk.NewWeb.Services;

public interface ISupportNotificationAdminClient
{
    Task<IReadOnlyList<SupportGroupDto>> GetGroupsAsync(string? owningOrganizationId = null);
    Task<SupportGroupDto?> CreateGroupAsync(CreateSupportGroupDto dto);
    Task<SupportGroupDto?> UpdateGroupAsync(string id, UpdateSupportGroupDto dto);
    Task<bool> DeleteGroupAsync(string id);

    Task<IReadOnlyList<SupportGroupMemberDto>> GetGroupMembersAsync(string groupId);
    Task<SupportGroupMemberDto?> CreateGroupMemberAsync(string groupId, CreateSupportGroupMemberDto dto);
    Task<SupportGroupMemberDto?> UpdateGroupMemberAsync(string groupId, string id, UpdateSupportGroupMemberDto dto);
    Task<bool> DeleteGroupMemberAsync(string groupId, string id);

    Task<IReadOnlyList<OrganizationSupportCoverageDto>> GetCoverageAsync(string? customerOrganizationId = null);
    Task<OrganizationSupportCoverageDto?> CreateCoverageAsync(CreateOrganizationSupportCoverageDto dto);
    Task<OrganizationSupportCoverageDto?> UpdateCoverageAsync(string id, UpdateOrganizationSupportCoverageDto dto);
    Task<bool> DeleteCoverageAsync(string id);

    Task<IReadOnlyList<SupportNotificationSubscriptionDto>> GetSubscriptionsAsync(string? customerOrganizationId = null);
    Task<SupportNotificationSubscriptionDto?> CreateSubscriptionAsync(CreateSupportNotificationSubscriptionDto dto);
    Task<SupportNotificationSubscriptionDto?> UpdateSubscriptionAsync(string id, UpdateSupportNotificationSubscriptionDto dto);
    Task<bool> DeleteSubscriptionAsync(string id);

    Task<SupportNotificationRecipientPreviewDto?> GetRecipientPreviewAsync(
        string organizationId,
        SupportNotificationEventType eventType);

    Task<SupportNotificationBootstrapResultDto?> BootstrapDefaultsAsync();
}

public sealed class SupportNotificationAdminClient(IHttpClientFactory httpClientFactory) : ISupportNotificationAdminClient
{
    private readonly HttpClient http = httpClientFactory.CreateClient("HelpdeskApi");

    public async Task<IReadOnlyList<SupportGroupDto>> GetGroupsAsync(string? owningOrganizationId = null)
    {
        var url = "api/v1/support/groups";
        if (!string.IsNullOrWhiteSpace(owningOrganizationId))
        {
            url += $"?owningOrganizationId={Uri.EscapeDataString(owningOrganizationId)}";
        }

        return await http.GetFromJsonAsync<List<SupportGroupDto>>(url) ?? [];
    }

    public async Task<SupportGroupDto?> CreateGroupAsync(CreateSupportGroupDto dto)
    {
        var response = await http.PostAsJsonAsync("api/v1/support/groups", dto);
        return await ReadResultAsync<SupportGroupDto>(response);
    }

    public async Task<SupportGroupDto?> UpdateGroupAsync(string id, UpdateSupportGroupDto dto)
    {
        var response = await http.PutAsJsonAsync($"api/v1/support/groups/{Uri.EscapeDataString(id)}", dto);
        return await ReadResultAsync<SupportGroupDto>(response);
    }

    public Task<bool> DeleteGroupAsync(string id)
        => DeleteAsync($"api/v1/support/groups/{Uri.EscapeDataString(id)}");

    public async Task<IReadOnlyList<SupportGroupMemberDto>> GetGroupMembersAsync(string groupId)
        => await http.GetFromJsonAsync<List<SupportGroupMemberDto>>(
               $"api/v1/support/groups/{Uri.EscapeDataString(groupId)}/members")
           ?? [];

    public async Task<SupportGroupMemberDto?> CreateGroupMemberAsync(string groupId, CreateSupportGroupMemberDto dto)
    {
        var response = await http.PostAsJsonAsync(
            $"api/v1/support/groups/{Uri.EscapeDataString(groupId)}/members",
            dto);
        return await ReadResultAsync<SupportGroupMemberDto>(response);
    }

    public async Task<SupportGroupMemberDto?> UpdateGroupMemberAsync(string groupId, string id, UpdateSupportGroupMemberDto dto)
    {
        var response = await http.PutAsJsonAsync(
            $"api/v1/support/groups/{Uri.EscapeDataString(groupId)}/members/{Uri.EscapeDataString(id)}",
            dto);
        return await ReadResultAsync<SupportGroupMemberDto>(response);
    }

    public Task<bool> DeleteGroupMemberAsync(string groupId, string id)
        => DeleteAsync($"api/v1/support/groups/{Uri.EscapeDataString(groupId)}/members/{Uri.EscapeDataString(id)}");

    public async Task<IReadOnlyList<OrganizationSupportCoverageDto>> GetCoverageAsync(string? customerOrganizationId = null)
    {
        var url = "api/v1/support/coverage";
        if (!string.IsNullOrWhiteSpace(customerOrganizationId))
        {
            url += $"?customerOrganizationId={Uri.EscapeDataString(customerOrganizationId)}";
        }

        return await http.GetFromJsonAsync<List<OrganizationSupportCoverageDto>>(url) ?? [];
    }

    public async Task<OrganizationSupportCoverageDto?> CreateCoverageAsync(CreateOrganizationSupportCoverageDto dto)
    {
        var response = await http.PostAsJsonAsync("api/v1/support/coverage", dto);
        return await ReadResultAsync<OrganizationSupportCoverageDto>(response);
    }

    public async Task<OrganizationSupportCoverageDto?> UpdateCoverageAsync(string id, UpdateOrganizationSupportCoverageDto dto)
    {
        var response = await http.PutAsJsonAsync($"api/v1/support/coverage/{Uri.EscapeDataString(id)}", dto);
        return await ReadResultAsync<OrganizationSupportCoverageDto>(response);
    }

    public Task<bool> DeleteCoverageAsync(string id)
        => DeleteAsync($"api/v1/support/coverage/{Uri.EscapeDataString(id)}");

    public async Task<IReadOnlyList<SupportNotificationSubscriptionDto>> GetSubscriptionsAsync(string? customerOrganizationId = null)
    {
        var url = "api/v1/support/notification-subscriptions";
        if (!string.IsNullOrWhiteSpace(customerOrganizationId))
        {
            url += $"?customerOrganizationId={Uri.EscapeDataString(customerOrganizationId)}";
        }

        return await http.GetFromJsonAsync<List<SupportNotificationSubscriptionDto>>(url) ?? [];
    }

    public async Task<SupportNotificationSubscriptionDto?> CreateSubscriptionAsync(CreateSupportNotificationSubscriptionDto dto)
    {
        var response = await http.PostAsJsonAsync("api/v1/support/notification-subscriptions", dto);
        return await ReadResultAsync<SupportNotificationSubscriptionDto>(response);
    }

    public async Task<SupportNotificationSubscriptionDto?> UpdateSubscriptionAsync(string id, UpdateSupportNotificationSubscriptionDto dto)
    {
        var response = await http.PutAsJsonAsync($"api/v1/support/notification-subscriptions/{Uri.EscapeDataString(id)}", dto);
        return await ReadResultAsync<SupportNotificationSubscriptionDto>(response);
    }

    public Task<bool> DeleteSubscriptionAsync(string id)
        => DeleteAsync($"api/v1/support/notification-subscriptions/{Uri.EscapeDataString(id)}");

    public Task<SupportNotificationRecipientPreviewDto?> GetRecipientPreviewAsync(
        string organizationId,
        SupportNotificationEventType eventType)
        => http.GetFromJsonAsync<SupportNotificationRecipientPreviewDto>(
            $"api/v1/support/organizations/{Uri.EscapeDataString(organizationId)}/recipient-preview?eventType={eventType}");

    public async Task<SupportNotificationBootstrapResultDto?> BootstrapDefaultsAsync()
    {
        var response = await http.PostAsync("api/v1/support/bootstrap/defaults", null);
        return await ReadResultAsync<SupportNotificationBootstrapResultDto>(response);
    }

    private async Task<bool> DeleteAsync(string url)
    {
        var response = await http.DeleteAsync(url);
        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        throw new InvalidOperationException(await ReadErrorAsync(response, "Delete failed."));
    }

    private static async Task<T?> ReadResultAsync<T>(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<T>();
        }

        throw new InvalidOperationException(await ReadErrorAsync(response, "Support notification request failed."));
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, string fallback)
    {
        var body = await response.Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(body)
            ? $"{fallback} HTTP {(int)response.StatusCode}."
            : $"{fallback} HTTP {(int)response.StatusCode}: {body}";
    }
}

public sealed class SupportNotificationBootstrapResultDto
{
    public int OrganizationsProcessed { get; set; }
    public int OrganizationsSkippedMissingProvider { get; set; }
    public int SupportGroupsCreated { get; set; }
    public int CoveragesCreated { get; set; }
    public int SubscriptionsCreated { get; set; }
}
