using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using Helpdesk.Shared.DTOs.Request;
using Helpdesk.Shared.DTOs.Service;
using Helpdesk.Shared.Models;

namespace HelpDesk.NewWeb.Services;

public interface IRequestService
{
    Task<IEnumerable<Service>> GetServicesAsync();
    Task<Service?> CreateServiceAsync(CreateServiceDto dto);
    Task<RequestDto?> SubmitRequestAsync(CreateRequestDto dto);
}

public class RequestService : IRequestService
{
    private readonly HttpClient _httpClient;

    public RequestService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("HelpdeskApi");
    }

    public async Task<IEnumerable<Service>> GetServicesAsync()
    {
        var services = await _httpClient.GetFromJsonAsync<IEnumerable<Service>>("/api/v1/services");
        return services ?? Enumerable.Empty<Service>();
    }

    public async Task<Service?> CreateServiceAsync(CreateServiceDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/services", dto);
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<Service>();
    }

    public async Task<RequestDto?> SubmitRequestAsync(CreateRequestDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/requests", dto);
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<RequestDto>();
    }
}

