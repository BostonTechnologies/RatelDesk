namespace Helpdesk.Shared.DTOs.Resources;

public class DatasetIngestCredentialDto
{
    public string Id { get; set; } = string.Empty;
    public string DatasetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset? LastUsedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public class CreateDatasetIngestCredentialDto
{
    public string Name { get; set; } = string.Empty;
}

public class DatasetIngestCredentialCreatedDto
{
    public DatasetIngestCredentialDto Credential { get; set; } = new();
    public string PlainTextKey { get; set; } = string.Empty;
}
