using System.Text.Json;
using Helpdesk.Shared.Connectivity;
using Xunit;

public class M2MConnectivityDtoTests
{
    [Fact]
    public void SettingsDto_Serializes_And_Deserializes()
    {
        var dto = new M2MConnectivitySettingsDto(true, "https://remote", "aud", "RemoteName");
        var json = JsonSerializer.Serialize(dto);
        var back = JsonSerializer.Deserialize<M2MConnectivitySettingsDto>(json);
        Assert.NotNull(back);
        Assert.True(back!.Enabled);
        Assert.Equal("https://remote", back.RemoteBaseUrl);
        Assert.Equal("aud", back.RemoteAudience);
        Assert.Equal("RemoteName", back.RemoteSystemName);
    }

    [Fact]
    public void TestResultDto_Serializes_And_Deserializes()
    {
        var now = DateTimeOffset.UtcNow;
        var probe = new M2MConnectivityProbeResultDto("RemoteHealth", "url", TrafficLight.Green, 200, 12, "OK", "Remote", now);
        var result = new M2MConnectivityTestResultDto(new[] { probe });
        var json = JsonSerializer.Serialize(result);
        var back = JsonSerializer.Deserialize<M2MConnectivityTestResultDto>(json);
        Assert.NotNull(back);
        Assert.Single(back!.Probes);
        Assert.Equal(TrafficLight.Green, back.Probes[0].Status);
    }
}

