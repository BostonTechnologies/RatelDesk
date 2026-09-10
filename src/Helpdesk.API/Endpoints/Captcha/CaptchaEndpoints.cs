using Helpdesk.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.API.Endpoints.Captcha;

public static class CaptchaEndpoints
{
    /// <summary>
    /// Maps the CAPTCHA-related endpoints to the specified endpoint route builder.
    /// </summary>
    /// <remarks>This method registers a group of endpoints under the route <c>/api/v1/captcha</c> with the
    /// tag "Captcha". It includes an endpoint for generating a CAPTCHA, which returns a CAPTCHA ID and
    /// challenge.</remarks>
    /// <param name="app">The <see cref="IEndpointRouteBuilder"/> to which the CAPTCHA endpoints will be added.</param>
    public static void MapCaptchaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/captcha").WithTags("Captcha");

        group.MapGet("/", ([FromServices] ICaptchaService svc) =>
        {
            var (id, challenge) = svc.GenerateCaptcha();
            var imageBytes = svc.RenderCaptchaImage(challenge);
            var imageBase64 = $"data:image/png;base64,{Convert.ToBase64String(imageBytes)}";
            return Results.Ok(new CaptchaResponse(id, challenge, imageBase64));
        }).AllowAnonymous();
    }
}

public record CaptchaResponse(Guid id, string challenge, string imageBase64);
