using System.Diagnostics;
using System.Threading;
using Helpdesk.Application.Events;
using Microsoft.AspNetCore.Http;

namespace Helpdesk.Infrastructure.Events;

public sealed class HttpCorrelationContext(IHttpContextAccessor httpContextAccessor) : ICorrelationContext
{
    private static readonly AsyncLocal<string?> AmbientCorrelationId = new();

    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public string GetCorrelationId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            if (httpContext.Items.TryGetValue(CorrelationConstants.HttpContextItemKey, out var value) &&
                value is string correlationId &&
                !string.IsNullOrWhiteSpace(correlationId))
            {
                AmbientCorrelationId.Value = correlationId;
                return correlationId;
            }

            var headerValue = httpContext.Request.Headers[CorrelationConstants.HeaderName].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(headerValue))
            {
                AmbientCorrelationId.Value = headerValue;
                return headerValue;
            }
        }

        if (!string.IsNullOrWhiteSpace(AmbientCorrelationId.Value))
            return AmbientCorrelationId.Value!;

        if (!string.IsNullOrWhiteSpace(Activity.Current?.Id))
        {
            AmbientCorrelationId.Value = Activity.Current!.Id;
            return AmbientCorrelationId.Value!;
        }

        AmbientCorrelationId.Value = $"corr-{Guid.NewGuid():N}";
        return AmbientCorrelationId.Value;
    }
}
