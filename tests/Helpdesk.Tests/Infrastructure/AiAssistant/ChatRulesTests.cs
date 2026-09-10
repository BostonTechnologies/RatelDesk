using Helpdesk.Application.AiAssistant.Chat;
using Helpdesk.Shared.AiAssistant.Chat;

namespace Helpdesk.Tests.Infrastructure.AiAssistant;

public sealed class ChatRulesTests
{
    [Theory]
    [InlineData(ChatState.Processing)]
    [InlineData(ChatState.AwaitingApproval)]
    [InlineData(ChatState.DeliveryUnknown)]
    [InlineData(ChatState.Archived)]
    public void BusyOrArchivedConversationRejectsMessages(ChatState state) => Assert.Throws<ChatConflictException>(() => ChatRules.RequireIdle(state));

    [Fact]
    public void IdleAdmitsMessage() => ChatRules.RequireIdle(ChatState.Idle);

    [Fact]
    public void ApprovalMustSelectOfferedKey()
    {
        var interaction = new AiAssistantChatInteraction();
        Assert.Throws<ArgumentException>(() => ChatRules.ValidateApproval(interaction, "invented", [new("deny", "Deny")]));
        ChatRules.ValidateApproval(interaction, "deny", [new("deny", "Deny")]);
    }

    [Fact]
    public void AnsweredApprovalCannotBeReused() => Assert.Throws<ChatConflictException>(() => ChatRules.ValidateApproval(new() { SelectedKey = "deny" }, "allow", [new("allow", "Allow")]));

    [Fact]
    public void ClientMessageIdIsRequired() => Assert.Throws<ArgumentException>(() => ChatRules.ValidateMessage(new(Guid.NewGuid(), Guid.Empty, "hello")));

    [Theory]
    [InlineData("/private/customer/report.txt", "report.txt")]
    [InlineData("C:\\private\\report.txt", "report.txt")]
    [InlineData("https://host/file?token=secret", "artifact")]
    public void FileMetadataCannotExposePaths(string input, string expected) => Assert.Equal(expected, ChatOutputSafety.FileName(input));

    [Theory]
    [InlineData("shell --password secret")]
    [InlineData("<script>alert(1)</script>")]
    public void ToolNamesAreAllowlisted(string input) => Assert.Equal("tool", ChatOutputSafety.Identifier(input));
}
