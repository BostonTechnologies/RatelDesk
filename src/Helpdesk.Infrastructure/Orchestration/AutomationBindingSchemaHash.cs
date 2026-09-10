using System.Security.Cryptography;
using System.Text;
using Helpdesk.Shared.DTOs.Orchestration;

namespace Helpdesk.Infrastructure.Orchestration;

internal static class AutomationBindingSchemaHash
{
    public static string Compute(IReadOnlyList<OrchestrationCatalogInputDefinitionDto> inputs)
    {
        var raw = string.Join(
            "||",
            inputs
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(x => $"{x.Order}|{x.Key}|{x.Label}|{x.Type}|{x.Required}|{x.DefaultValue}|{x.HelpText}|{x.OptionsJson}"));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }
}
