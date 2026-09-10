using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using Helpdesk.Shared.DTOs.RequestForm;

namespace HelpDesk.NewWeb.Services;

public interface IRequestFormService
{
    Task<IEnumerable<RequestFormDto>> GetFormsAsync(string serviceId);
    Task<JsonDocument?> GetFormSchemaAsync(string serviceId, string formId);
}

public class RequestFormService : IRequestFormService
{
    private readonly HttpClient _httpClient;

    public RequestFormService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("HelpdeskApi");
    }

    public async Task<IEnumerable<RequestFormDto>> GetFormsAsync(string serviceId)
    {
        var forms = await _httpClient.GetFromJsonAsync<IEnumerable<RequestFormDto>>($"/api/v1/services/{serviceId}/forms");
        return forms ?? Enumerable.Empty<RequestFormDto>();
    }

    public async Task<JsonDocument?> GetFormSchemaAsync(string serviceId, string formId)
    {
        var forms = await GetFormsAsync(serviceId);
        var form = forms.FirstOrDefault(f => f.Id == formId);
        if (form == null || string.IsNullOrWhiteSpace(form.JsonSchema))
            return null;
        try
        {
            return JsonDocument.Parse(form.JsonSchema);
        }
        catch
        {
            return null;
        }
    }
}

