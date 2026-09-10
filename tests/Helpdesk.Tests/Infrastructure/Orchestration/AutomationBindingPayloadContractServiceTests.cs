using System.Text.Json;
using Helpdesk.Application.Orchestration;
using Helpdesk.Application.RequestTasks;
using Helpdesk.Infrastructure.Orchestration;
using Helpdesk.Shared.DTOs.Orchestration;
using Helpdesk.Shared.DTOs.RequestForm;
using Helpdesk.Shared.Models;
using NSubstitute;

namespace Helpdesk.Tests.Infrastructure.Orchestration;

public sealed class AutomationBindingPayloadContractServiceTests
{
    [Fact]
    public async Task ValidateBoundRequestPayloadAsync_Fails_WhenRequiredMappedFieldIsMissing()
    {
        var parser = new RequestFormSchemaParser();
        var bindings = Substitute.For<IAutomationBindingService>();
        bindings.ListAsync("form-1", Arg.Any<CancellationToken>())
            .Returns([
                new AutomationBindingDto
                {
                    Id = "binding-1",
                    RequestFormId = "form-1",
                    TaskTemplateId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    OrchestrationRequestDefinitionId = "orchestration-req-1",
                    Enabled = true
                }
            ]);

        var service = new AutomationBindingPayloadContractService(parser, bindings);
        var form = CreateRequestForm(
            """
            {
              "fields": [
                { "key": "user_email", "label": "User Email", "type": "text", "required": true }
              ],
              "tasks": [
                {
                  "id": "11111111-1111-1111-1111-111111111111",
                  "name": "Provision mailbox",
                  "type": "automation",
                  "payloadMapping": {
                    "userEmail": "user_email"
                  }
                }
              ]
            }
            """);

        var result = await service.ValidateBoundRequestPayloadAsync(form, "{}");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, x => x.Contains("user_email", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildTaskInput_OmitsMissingOptionalFields_AndPreservesPresentValues()
    {
        var parser = new RequestFormSchemaParser();
        var bindings = Substitute.For<IAutomationBindingService>();
        var service = new AutomationBindingPayloadContractService(parser, bindings);
        var form = CreateRequestForm(
            """
            {
              "fields": [
                { "key": "user_email", "label": "User Email", "type": "text", "required": true },
                { "key": "department", "label": "Department", "type": "text", "required": false }
              ],
              "tasks": [
                {
                  "id": "11111111-1111-1111-1111-111111111111",
                  "name": "Provision mailbox",
                  "type": "automation",
                  "payloadMapping": {
                    "userEmail": "user_email",
                    "department": "department"
                  }
                }
              ]
            }
            """);

        var taskTemplate = parser.Parse(form.JsonSchema).Tasks.Single();

        var result = service.BuildTaskInput(form, taskTemplate, "{\"user_email\":\"person@example.com\"}");

        Assert.True(result.Success);
        Assert.Equal("person@example.com", result.InputNode["userEmail"]?.GetValue<string>());
        Assert.Null(result.InputNode["department"]);
    }

    [Fact]
    public void BuildTaskInput_Succeeds_ForGeneratedLabelKeys()
    {
        var parser = new RequestFormSchemaParser();
        var bindings = Substitute.For<IAutomationBindingService>();
        var service = new AutomationBindingPayloadContractService(parser, bindings);
        var form = CreateRequestForm(
            """
            {
              "fields": [
                { "label": "path1", "type": "text", "required": true },
                { "label": "path2", "type": "text", "required": true }
              ],
              "tasks": [
                {
                  "id": "11111111-1111-1111-1111-111111111111",
                  "name": "Run param job",
                  "type": "automation",
                  "payloadMapping": {
                    "path1": "path1",
                    "path2": "path2"
                  }
                }
              ]
            }
            """);

        var taskTemplate = parser.Parse(form.JsonSchema).Tasks.Single();

        var result = service.BuildTaskInput(form, taskTemplate, "{\"path1\":\"c:\\\\\",\"path2\":\"c:\\\\DeepStack\"}");

        Assert.True(result.Success);
        Assert.Equal("c:\\", result.InputNode["path1"]?.GetValue<string>());
        Assert.Equal("c:\\DeepStack", result.InputNode["path2"]?.GetValue<string>());
    }

    [Fact]
    public void BuildTaskInput_PreservesScalarDatasetBoundKeys()
    {
        var parser = new RequestFormSchemaParser();
        var bindings = Substitute.For<IAutomationBindingService>();
        var service = new AutomationBindingPayloadContractService(parser, bindings);
        var form = CreateRequestForm(
            """
            {
              "fields": [
                {
                  "key": "location",
                  "label": "Location",
                  "type": "text",
                  "required": true,
                  "dataBinding": {
                    "datasetId": "dataset-1",
                    "displayColumn": "name",
                    "searchColumns": ["name"]
                  }
                }
              ],
              "tasks": [
                {
                  "id": "11111111-1111-1111-1111-111111111111",
                  "name": "Provision by location",
                  "type": "automation",
                  "payloadMapping": {
                    "locationKey": "location"
                  }
                }
              ]
            }
            """);

        var taskTemplate = parser.Parse(form.JsonSchema).Tasks.Single();

        var result = service.BuildTaskInput(
            form,
            taskTemplate,
            "{\"location\":\"loc-123\",\"_boundLabels\":{\"location\":\"Cape Town\"}}");

        Assert.True(result.Success);
        Assert.Equal("loc-123", result.InputNode["locationKey"]?.GetValue<string>());
    }

    [Fact]
    public void BuildTaskInput_TreatsHelpdeskDataAsTextLikeMappedField()
    {
        var parser = new RequestFormSchemaParser();
        var bindings = Substitute.For<IAutomationBindingService>();
        var service = new AutomationBindingPayloadContractService(parser, bindings);
        var form = CreateRequestForm(
            """
            {
              "fields": [
                {
                  "key": "asset",
                  "label": "Asset",
                  "type": "helpdeskData",
                  "required": true,
                  "dataBinding": {
                    "datasetId": "dataset-1",
                    "displayColumn": "name",
                    "searchColumns": ["name"]
                  }
                }
              ],
              "tasks": [
                {
                  "id": "11111111-1111-1111-1111-111111111111",
                  "name": "Provision asset",
                  "type": "automation",
                  "payloadMapping": {
                    "assetId": "asset"
                  }
                }
              ]
            }
            """);

        var taskTemplate = parser.Parse(form.JsonSchema).Tasks.Single();

        var result = service.BuildTaskInput(form, taskTemplate, "{\"asset\":\"asset-123\"}");

        Assert.True(result.Success);
        Assert.Equal("asset-123", result.InputNode["assetId"]?.GetValue<string>());
    }

    [Fact]
    public void BuildTaskInput_TreatsPredefinedAsTextLikeMappedField()
    {
        var parser = new RequestFormSchemaParser();
        var bindings = Substitute.For<IAutomationBindingService>();
        var service = new AutomationBindingPayloadContractService(parser, bindings);
        var form = CreateRequestForm(
            """
            {
              "fields": [
                {
                  "key": "path1",
                  "label": "Path",
                  "type": "predefined",
                  "required": true,
                  "predefinedOptions": ["c:\\", "c:\\apps"]
                }
              ],
              "tasks": [
                {
                  "id": "11111111-1111-1111-1111-111111111111",
                  "name": "Run script",
                  "type": "automation",
                  "payloadMapping": {
                    "Path": "path1"
                  }
                }
              ]
            }
            """);

        var taskTemplate = parser.Parse(form.JsonSchema).Tasks.Single();

        var result = service.BuildTaskInput(form, taskTemplate, "{\"path1\":\"c:\\\\apps\"}");

        Assert.True(result.Success);
        Assert.Equal("c:\\apps", result.InputNode["Path"]?.GetValue<string>());
    }

    [Fact]
    public void BuildTaskInput_Fails_WhenRequiredPredefinedMappedFieldIsEmpty()
    {
        var parser = new RequestFormSchemaParser();
        var bindings = Substitute.For<IAutomationBindingService>();
        var service = new AutomationBindingPayloadContractService(parser, bindings);
        var form = CreateRequestForm(
            """
            {
              "fields": [
                {
                  "key": "path1",
                  "label": "Path",
                  "type": "predefined",
                  "required": true,
                  "predefinedOptions": ["c:\\", "c:\\apps"]
                }
              ],
              "tasks": [
                {
                  "id": "11111111-1111-1111-1111-111111111111",
                  "name": "Run script",
                  "type": "automation",
                  "payloadMapping": {
                    "Path": "path1"
                  }
                }
              ]
            }
            """);

        var taskTemplate = parser.Parse(form.JsonSchema).Tasks.Single();

        var result = service.BuildTaskInput(form, taskTemplate, "{\"path1\":\"\"}");

        Assert.False(result.Success);
        Assert.Contains("path1", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildTaskInput_Fails_WhenMappingsResolveNoOrchestrationInputs()
    {
        var parser = new RequestFormSchemaParser();
        var bindings = Substitute.For<IAutomationBindingService>();
        var service = new AutomationBindingPayloadContractService(parser, bindings);
        var form = CreateRequestForm(
            """
            {
              "fields": [
                { "key": "path", "label": "Path", "type": "text", "required": false }
              ],
              "tasks": [
                {
                  "id": "11111111-1111-1111-1111-111111111111",
                  "name": "Run script",
                  "type": "automation",
                  "payloadMapping": {
                    "Path": "path"
                  }
                }
              ]
            }
            """);

        var taskTemplate = parser.Parse(form.JsonSchema).Tasks.Single();

        var result = service.BuildTaskInput(form, taskTemplate, "{}");

        Assert.False(result.Success);
        Assert.Contains("Run script", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    private static RequestForm CreateRequestForm(string schemaJson)
    {
        return new RequestForm
        {
            Id = "form-1",
            Title = "Automation Request",
            JsonSchema = JsonDocument.Parse(schemaJson)
        };
    }
}
