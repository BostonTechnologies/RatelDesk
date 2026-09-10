using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using Helpdesk.Shared.DTOs;
using Helpdesk.Shared.DTOs.AI;

namespace HelpDesk.NewWeb.Services;

public interface IAiAdminClient
{
    Task<PagedResponse<AiProviderSummaryDto>> GetProvidersAsync(int page, int pageSize);
    Task<AiProviderSummaryDto?> GetProviderAsync(string id);
    Task<AiProviderSummaryDto?> CreateProviderAsync(AiProviderCreateDto dto);
    Task<AiProviderSummaryDto?> UpdateProviderAsync(string id, AiProviderUpdateDto dto);
    Task<bool> DeleteProviderAsync(string id);
    Task<AiProviderTestResultDto> TestAsync(string id);
    Task<IEnumerable<AiModelDto>> GetModelsAsync(string id);
    Task<string?> ChatTestAsync(AiTestChatRequest request);
}

public class AiAdminClient : IAiAdminClient
{
    private readonly HttpClient _httpClient;

    public AiAdminClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("HelpdeskApi");
    }

    public async Task<PagedResponse<AiProviderSummaryDto>> GetProvidersAsync(int page, int pageSize)
    {
        var result = await _httpClient.GetFromJsonAsync<PagedResponse<AiProviderSummaryDto>>($"/api/v1/ai/providers?page={page}&pageSize={pageSize}");
        return result ?? new PagedResponse<AiProviderSummaryDto> { Page = page, PageSize = pageSize };
    }

    public async Task<AiProviderSummaryDto?> GetProviderAsync(string id)
    {
        return await _httpClient.GetFromJsonAsync<AiProviderSummaryDto>($"/api/v1/ai/providers/{id}");
    }

    public async Task<AiProviderSummaryDto?> CreateProviderAsync(AiProviderCreateDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/ai/providers", dto);
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<AiProviderSummaryDto>();
    }

    public async Task<AiProviderSummaryDto?> UpdateProviderAsync(string id, AiProviderUpdateDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/ai/providers/{id}", dto);
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<AiProviderSummaryDto>();
    }

    public async Task<bool> DeleteProviderAsync(string id)
    {
        var response = await _httpClient.DeleteAsync($"/api/v1/ai/providers/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<AiProviderTestResultDto> TestAsync(string id)
    {
        var response = await _httpClient.PostAsync($"/api/v1/ai/providers/{id}/test", null);
        if (!response.IsSuccessStatusCode)
        {
            return new AiProviderTestResultDto
            {
                Success = false,
                Message = $"Provider test request failed with HTTP {(int)response.StatusCode}."
            };
        }

        var result = await response.Content.ReadFromJsonAsync<AiProviderTestResultDto>();
        return result ?? new AiProviderTestResultDto { Success = false, Message = "Provider test returned no result." };
    }

    public async Task<IEnumerable<AiModelDto>> GetModelsAsync(string id)
    {
        var response = await _httpClient.GetAsync($"/api/v1/ai/providers/{id}/models");
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(body)
                ? $"Model refresh failed with HTTP {(int)response.StatusCode}."
                : body);
        }

        var models = await response.Content.ReadFromJsonAsync<IEnumerable<AiModelDto>>();
        return models ?? Enumerable.Empty<AiModelDto>();
    }

    public async Task<string?> ChatTestAsync(AiTestChatRequest request)
    {
        var dto = new AiChatRequestDto { Prompt = request.Prompt, Model = request.Model };
        var response = await _httpClient.PostAsJsonAsync($"/api/v1/ai/providers/{request.ProviderId}/chat-test", dto);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(body)
                ? $"Chat test failed with HTTP {(int)response.StatusCode}."
                : body);
        }

        var result = await response.Content.ReadFromJsonAsync<ChatResponse>();
        return result?.Response;
    }

    private record ChatResponse(string Response);
}
