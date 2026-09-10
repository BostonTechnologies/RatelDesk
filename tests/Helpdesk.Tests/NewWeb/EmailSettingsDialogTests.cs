namespace Helpdesk.Tests.NewWeb;

public class EmailSettingsDialogTests
{
    private static readonly string Source = ReadDialogSource();

    [Fact]
    public void DialogLoadsFirstEmailSettingsRecord()
    {
        Assert.Contains("GetFromJsonAsync<List<EmailInboxSettingsForm>>(\"/api/v1/email-settings\")", Source);
        Assert.Contains("Model = settings[0];", Source);
        Assert.Contains("Model.ClientSecret = string.Empty;", Source);
    }

    [Fact]
    public void DialogSavesExistingRecordWithPut()
    {
        Assert.Contains("Api.PutAsJsonAsync($\"/api/v1/email-settings/{Model.Id}\"", Source);
        Assert.DoesNotContain("/api/v1/email/imap/save", Source);
    }

    [Fact]
    public void DialogTestsConnectionWithoutLegacyImapEndpoint()
    {
        Assert.Contains("Api.PostAsJsonAsync(\"/api/v1/email-settings/test\"", Source);
        Assert.DoesNotContain("/api/v1/email/imap/test", Source);
    }

    [Fact]
    public void DialogBindsMailboxSslAndEnabledFields()
    {
        Assert.Contains("@bind-Value=\"Model.MailboxAddress\"", Source);
        Assert.Contains("@bind-Value=\"Model.UseSsl\"", Source);
        Assert.Contains("@bind-Value=\"Model.Enabled\"", Source);
        Assert.Contains("@bind-Value=\"Model.BackgroundSyncEnabled\"", Source);
        Assert.Contains("Label=\"Mailbox enabled\"", Source);
        Assert.Contains("Label=\"Background ingestion enabled\"", Source);
    }

    [Fact]
    public void BlankClientSecretIsNotSentAsANewSecret()
    {
        Assert.Contains("ClientSecret = string.IsNullOrWhiteSpace(Model.ClientSecret) ? null : Model.ClientSecret", Source);
        Assert.Contains("Leave blank to keep the existing secret.", Source);
    }

    private static string ReadDialogSource()
    {
        var path = Path.Combine(
            TestEnvironment.RepositoryRoot,
            "src",
            "HelpDesk.NewWeb",
            "Components",
            "Dialogs",
            "Settings",
            "EmailSettingsDialog.razor");
        return File.ReadAllText(path);
    }
}
