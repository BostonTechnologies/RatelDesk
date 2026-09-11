extern alias NewWeb;

using System.Text.Json;
using NewWeb::HelpDesk.NewWeb.Models;

namespace Helpdesk.Tests.NewWeb;

public class InstanceBrandingAdministrationResponseTests
{
    [Theory]
    [InlineData("2")]
    [InlineData("\"Environment\"")]
    public void Deserialization_AcceptsNumericAndStringValueSources(string source)
    {
        var json = $$"""
            {
              "effective": {},
              "fields": [
                {
                  "name": "ApplicationName",
                  "source": {{source}},
                  "isAdminEditable": false
                }
              ]
            }
            """;

        var response = JsonSerializer.Deserialize<InstanceBrandingAdministrationResponse>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var field = Assert.Single(response!.Fields);
        Assert.Equal("ApplicationName", field.Name);
        Assert.Equal(InstanceBrandingValueSource.Environment, field.Source);
        Assert.False(field.IsAdminEditable);
    }
}
