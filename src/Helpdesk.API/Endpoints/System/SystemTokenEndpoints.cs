using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Helpdesk.Shared.Build;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Helpdesk.API.Endpoints.System;

public static class SystemTokenEndpoints
{
    // One helper so signing == validation
    private static SymmetricSecurityKey BuildSymmetricKey(string secret)
    {
        // If you store hex/base64, decode; otherwise fall back to UTF8.
        try { return new SymmetricSecurityKey(Convert.FromHexString(secret)); } catch { }
        try { return new SymmetricSecurityKey(Convert.FromBase64String(secret)); } catch { }
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    }

    public static void MapSystemTokenEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/system").WithTags("System");

        group.MapGet("/version", (IHostEnvironment environment) =>
        {
            return Results.Ok(BuildInfoProvider.FromAssembly(typeof(SystemTokenEndpoints).Assembly, environment.EnvironmentName));
        })
        .AllowAnonymous()
        .WithName("GetSystemVersion")
        .WithSummary("Get API version")
        .WithDescription("Returns Helpdesk API release and environment information.");

        group.MapPost("/token", (
            HttpContext http,
            [FromHeader(Name = "X-System-Secret")] string secret,
            IConfiguration config,
            IHostEnvironment environment) =>
        {
            // 1) Authenticate caller with the shared secret
            var expected = config["SYSTEM_TOKEN_SECRET"] ?? config["SystemTokenSecret"];
            if (string.IsNullOrWhiteSpace(expected) || secret != expected)
                return Results.Unauthorized();

            // 2) Mint token using the SAME values the "System" scheme validates
            var issuer = config["SystemToken:Issuer"];    // e.g. https://id.example.com/application/o/rateldesk/
            var audience = config["SystemToken:Audience"];  // e.g. 6LXRqcYgWZFB3ZxvQ4I0FbEhEmkOMWcWZneXaAyH
            var key = BuildSymmetricKey(expected);     // uses SystemTokenSecret
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new(ClaimTypes.Role, "system.blazor-web"),
                new("token_use", "system"),
                new("preferred_username", "system.blazor-web")
            };

            if (environment.IsDevelopment() && config.GetValue<bool>("DevelopmentOperator:Enabled"))
            {
                var displayName = config["DevelopmentOperator:DisplayName"] ?? "Codex Local Operator";
                var preferredUsername = config["DevelopmentOperator:PreferredUsername"] ?? "codex@localhost";
                var subject = config["DevelopmentOperator:Subject"] ?? "development-operator";
                var groups = config.GetSection("DevelopmentOperator:Groups").Get<string[]>() ?? ["HelpdeskAdmin"];

                claims.Add(new Claim(JwtRegisteredClaimNames.Sub, subject));
                claims.Add(new Claim("auth_mode", "development"));
                claims.Add(new Claim("preferred_username", preferredUsername));
                claims.Add(new Claim("name", displayName));

                foreach (var groupName in groups.Where(g => !string.IsNullOrWhiteSpace(g)).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    claims.Add(new Claim("groups", groupName));
                    claims.Add(new Claim("roles", groupName));
                    claims.Add(new Claim(ClaimTypes.Role, groupName));
                }
            }

            var jwt = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(10),
                signingCredentials: creds);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(jwt);
            return Results.Ok(new { token = tokenString });
        });
    }

}
