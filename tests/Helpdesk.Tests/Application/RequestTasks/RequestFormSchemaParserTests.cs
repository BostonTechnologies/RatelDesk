using System.Text.Json;
using Helpdesk.Application.RequestTasks;

namespace Helpdesk.Tests.Application.RequestTasks;

public class RequestFormSchemaParserTests
{
    [Fact]
    public void Parse_WithNoTasksArray_ReturnsEmptyTasks()
    {
        var parser = new RequestFormSchemaParser();
        using var doc = JsonDocument.Parse("{\"fields\":[]}");

        var result = parser.Parse(doc);

        Assert.NotNull(result);
        Assert.Empty(result.Tasks);
    }

    [Fact]
    public void Parse_WithTasksArray_DeserializesTemplates()
    {
        var parser = new RequestFormSchemaParser();
        using var doc = JsonDocument.Parse(
            """
            {
              "fields": [],
              "tasks": [
                {
                  "id": "11111111-1111-1111-1111-111111111111",
                  "name": "Manager approval",
                  "description": "Approve access",
                  "order": 2,
                  "type": "automation",
                  "defaultAssigneeId": "user-123",
                  "orchestratorJobName": "job.create.mailbox",
                  "payloadMapping": {
                    "userEmail": "user_email"
                  },
                  "expectedRuntimeMinutes": 30,
                  "graceRuntimeMinutes": 10,
                  "autoStart": true
                }
              ]
            }
            """);

        var result = parser.Parse(doc);

        Assert.Single(result.Tasks);
        var task = result.Tasks[0];
        Assert.Equal("Manager approval", task.Name);
        Assert.Equal("Approve access", task.Description);
        Assert.Equal(2, task.Order);
        Assert.Equal("automation", task.Type);
        Assert.Equal("user-123", task.DefaultAssigneeId);
        Assert.Equal("job.create.mailbox", task.OrchestratorJobName);
        Assert.NotNull(task.PayloadMapping);
        Assert.Equal("user_email", task.PayloadMapping!["userEmail"]);
        Assert.Equal(30, task.ExpectedRuntimeMinutes);
        Assert.Equal(10, task.GraceRuntimeMinutes);
        Assert.True(task.AutoStart);
    }

    [Fact]
    public void Parse_WithApprovalTask_DeserializesApprovalSettings()
    {
        var parser = new RequestFormSchemaParser();
        using var doc = JsonDocument.Parse(
            """
            {
              "fields": [],
              "tasks": [
                {
                  "id": "11111111-1111-1111-1111-111111111111",
                  "name": "Get manager approval",
                  "order": 1,
                  "type": "approval",
                  "approvalAllowedDays": 5,
                  "approvalApprovers": [
                    {
                      "source": "User",
                      "id": "user-1",
                      "name": "Jane Manager",
                      "email": "jane@example.com",
                      "organizationId": "org-1",
                      "organizationName": "Example Organization"
                    }
                  ]
                }
              ]
            }
            """);

        var result = parser.Parse(doc);

        var task = Assert.Single(result.Tasks);
        Assert.Equal("approval", task.Type);
        Assert.Equal(5, task.ApprovalAllowedDays);
        var approver = Assert.Single(task.ApprovalApprovers);
        Assert.Equal("User", approver.Source);
        Assert.Equal("user-1", approver.Id);
        Assert.Equal("Jane Manager", approver.Name);
        Assert.Equal("jane@example.com", approver.Email);
        Assert.Equal("org-1", approver.OrganizationId);
        Assert.Equal("Example Organization", approver.OrganizationName);
    }

    [Fact]
    public void Parse_WithDataBinding_DeserializesFieldBinding()
    {
        var parser = new RequestFormSchemaParser();
        using var doc = JsonDocument.Parse(
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
                    "searchColumns": ["name", "areaCode"],
                    "placeholder": "Search location",
                    "allowFreeText": false,
                    "minSearchLength": 2
                  }
                }
              ]
            }
            """);

        var result = parser.Parse(doc);

        var field = Assert.Single(result.Fields);
        Assert.NotNull(field.DataBinding);
        Assert.Equal("dataset-1", field.DataBinding!.DatasetId);
        Assert.Equal("name", field.DataBinding.DisplayColumn);
        Assert.Equal(["name", "areaCode"], field.DataBinding.SearchColumns);
        Assert.Equal("Search location", field.DataBinding.Placeholder);
        Assert.False(field.DataBinding.AllowFreeText);
        Assert.Equal(2, field.DataBinding.MinSearchLength);
    }

    [Fact]
    public void Parse_WithPredefinedOptions_DeserializesStaticOptions()
    {
        var parser = new RequestFormSchemaParser();
        using var doc = JsonDocument.Parse(
            """
            {
              "fields": [
                {
                  "key": "path",
                  "label": "Path",
                  "type": "predefined",
                  "required": true,
                  "predefinedOptions": ["c:\\", "c:\\apps"]
                }
              ]
            }
            """);

        var result = parser.Parse(doc);

        var field = Assert.Single(result.Fields);
        Assert.Equal("predefined", field.Type);
        Assert.Equal(["c:\\", "c:\\apps"], field.PredefinedOptions);
        Assert.Null(field.DataBinding);
    }

    [Fact]
    public void Parse_WithoutPredefinedOptions_KeepsEmptyOptionList()
    {
        var parser = new RequestFormSchemaParser();
        using var doc = JsonDocument.Parse(
            """
            {
              "fields": [
                {
                  "key": "path",
                  "label": "Path",
                  "type": "text",
                  "required": true
                }
              ]
            }
            """);

        var result = parser.Parse(doc);

        var field = Assert.Single(result.Fields);
        Assert.NotNull(field.PredefinedOptions);
        Assert.Empty(field.PredefinedOptions);
    }
}
