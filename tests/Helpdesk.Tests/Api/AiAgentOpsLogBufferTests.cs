using Helpdesk.API.Ops;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Helpdesk.Tests.Api;

public sealed class AiAgentOpsLogBufferTests
{
    [Fact]
    public void Query_Redacts_Common_Secret_Patterns()
    {
        var buffer = new AiAgentOpsLogBuffer();

        buffer.Add(
            LogLevel.Warning,
            "Helpdesk.Tests",
            new EventId(7, "SecretTest"),
            "Authorization: Bearer abc.def.ghi password=secret client_secret=hidden Cookie: session=value",
            new InvalidOperationException("Host=db;Username=app;Password=dbsecret;Database=helpdesk"));

        var result = buffer.Query(null, null, null, null, 10);
        var item = Assert.Single(result.Items);

        Assert.True(result.Redacted);
        Assert.DoesNotContain("abc.def.ghi", item.Message);
        Assert.DoesNotContain("secret", item.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden", item.Message);
        Assert.DoesNotContain("session=value", item.Message);
        Assert.DoesNotContain("dbsecret", item.ExceptionMessage);
    }

    [Fact]
    public void Query_Enforces_Hard_Limit_And_Since_Filter()
    {
        var buffer = new AiAgentOpsLogBuffer();
        for (var i = 0; i < 600; i++)
        {
            buffer.Add(LogLevel.Information, "Helpdesk.Tests", new EventId(0), $"message {i}", null);
        }

        var result = buffer.Query(500, null, null, null, 999);

        Assert.Equal(100, result.Items.Count);
        Assert.All(result.Items, item => Assert.True(item.Id > 500));
    }
}
