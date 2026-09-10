namespace Helpdesk.Application.Sla;

public class GeneratedReport
{
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public byte[]? CsvBytes { get; set; }
    public byte[]? ExcelBytes { get; set; }
    public string CsvFileName { get; set; } = "sla-report.csv";
    public string ExcelFileName { get; set; } = "sla-report.xlsx";
}
