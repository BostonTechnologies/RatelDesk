extern alias NewWeb;

using System.Text.Json;
using System.Text.Json.Nodes;
using Helpdesk.Shared.DTOs.RequestForm;
using NewWeb::HelpDesk.NewWeb.Services;

namespace Helpdesk.Tests.NewWeb;

public sealed class RequestFormTaskEditorStateTests
{
    [Fact]
    public void JsonOptions_LoadCamelCaseApprovalTask_PreservesApprovalSettings()
    {
        var json = """
        [
          {
            "id": "11111111-1111-1111-1111-111111111111",
            "name": "Get approval",
            "order": 2,
            "type": "approval",
            "autoStart": false,
            "approvalAllowedDays": 9,
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
        """;

        var tasks = JsonSerializer.Deserialize<List<RequestTaskTemplateModel>>(
            json,
            RequestFormTaskEditorState.CreateJsonOptions());

        var task = Assert.Single(RequestFormTaskEditorState.NormalizeTaskOrder(tasks!));
        Assert.Equal("approval", task.Type);
        Assert.True(task.AutoStart);
        Assert.Equal(9, task.ApprovalAllowedDays);
        var approver = Assert.Single(task.ApprovalApprovers);
        Assert.Equal("User", approver.Source);
        Assert.Equal("user-1", approver.Id);
        Assert.Equal("Jane Manager", approver.Name);
        Assert.Equal("jane@example.com", approver.Email);
        Assert.Equal("org-1", approver.OrganizationId);
        Assert.Equal("Example Organization", approver.OrganizationName);

        var node = JsonSerializer.SerializeToNode(task, RequestFormTaskEditorState.CreateJsonOptions())!.AsObject();
        Assert.Equal("approval", node["type"]!.GetValue<string>());
        Assert.Equal(9, node["approvalAllowedDays"]!.GetValue<int>());
        Assert.Equal("jane@example.com", node["approvalApprovers"]!.AsArray()[0]!["email"]!.GetValue<string>());
    }

    [Fact]
    public void NormalizeTaskOrder_InvalidTaskType_FallsBackToManual()
    {
        var task = new RequestTaskTemplateModel
        {
            Id = Guid.NewGuid(),
            Name = "Broken type",
            Order = 1,
            Type = "approval-ish"
        };

        var normalized = Assert.Single(RequestFormTaskEditorState.NormalizeTaskOrder(new[] { task }));

        Assert.Equal("manual", normalized.Type);
    }

    [Fact]
    public void MoveTaskToIndex_MovesSecondTaskToTop_AndRenumbersWithoutDroppingDependencies()
    {
        var approvalId = Guid.NewGuid();
        var automationId = Guid.NewGuid();
        var manualId = Guid.NewGuid();
        var tasks = new[]
        {
            new RequestTaskTemplateModel
            {
                Id = automationId,
                Name = "Automation",
                Order = 1,
                Type = "automation"
            },
            new RequestTaskTemplateModel
            {
                Id = approvalId,
                Name = "Approval",
                Order = 2,
                Type = "approval",
                DependsOn = new List<Guid> { automationId },
                ApprovalApprovers = new List<RequestTaskApprovalApproverModel>
                {
                    new() { Source = "User", Id = "user-1", Name = "Jane", Email = "jane@example.com" }
                }
            },
            new RequestTaskTemplateModel
            {
                Id = manualId,
                Name = "Manual",
                Order = 3,
                Type = "manual",
                DependsOn = new List<Guid> { approvalId }
            }
        };

        var reordered = RequestFormTaskEditorState.MoveTaskToIndex(tasks, approvalId, 0);

        Assert.Equal(new[] { approvalId, automationId, manualId }, reordered.Select(x => x.Id).ToArray());
        Assert.Equal(new[] { 1, 2, 3 }, reordered.Select(x => x.Order).ToArray());
        Assert.Equal("approval", reordered[0].Type);
        Assert.Equal(new[] { automationId }, reordered[0].DependsOn);
        Assert.Equal(new[] { approvalId }, reordered[2].DependsOn);
    }

    [Fact]
    public void MoveTaskToIndex_ButtonAndDropZoneMovement_ProduceSameOrder()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var thirdId = Guid.NewGuid();
        var tasks = new[]
        {
            new RequestTaskTemplateModel { Id = firstId, Name = "First", Order = 1, Type = "manual" },
            new RequestTaskTemplateModel { Id = secondId, Name = "Second", Order = 2, Type = "approval" },
            new RequestTaskTemplateModel { Id = thirdId, Name = "Third", Order = 3, Type = "automation" }
        };

        var fromDropZone = RequestFormTaskEditorState.MoveTaskToIndex(tasks, secondId, 0);
        var fromUpButton = RequestFormTaskEditorState.MoveTaskToIndex(tasks, secondId, 0);

        Assert.Equal(fromDropZone.Select(x => x.Id), fromUpButton.Select(x => x.Id));
        Assert.Equal(new[] { secondId, firstId, thirdId }, fromUpButton.Select(x => x.Id).ToArray());
    }
}
