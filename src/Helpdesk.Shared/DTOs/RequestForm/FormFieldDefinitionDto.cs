namespace Helpdesk.Shared.DTOs.RequestForm;

public enum FormFieldTypeDto { Text, TextArea, Number, Boolean, Select, Radio, Date, DateTime, Heading }

public sealed class FormFieldDefinitionDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public FormFieldTypeDto Type { get; set; }
    public string Label { get; set; } = "";
    public string? Key { get; set; }
    public string? HelpText { get; set; }
    public bool IsRequired { get; set; }
    public int Order { get; set; }
    public List<string> Options { get; set; } = new(); // Select/Radio
    public double? Min { get; set; }
    public double? Max { get; set; }
    public double? Step { get; set; }
}
