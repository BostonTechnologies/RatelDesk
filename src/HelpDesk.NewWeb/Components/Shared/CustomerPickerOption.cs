namespace HelpDesk.NewWeb.Components.Shared;

using Helpdesk.Shared.DTOs.Customer;

public sealed class CustomerPickerOption
{
    public CustomerDto? Customer { get; init; }
    public bool IsCreateOption { get; init; }
    public string SearchText { get; init; } = string.Empty;

    public string Label => IsCreateOption
        ? $"Create user \"{SearchText}\""
        : Customer is null
            ? string.Empty
            : $"{Customer.Name} ({Customer.Email})";
}
