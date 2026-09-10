namespace Helpdesk.Tests.NewWeb;

public class IncidentRelationTargetPickerDialogTests
{
    [Fact]
    public void Picker_UsesMudTableBuiltInMultiSelectionPattern()
    {
        var pickerPath = Path.Combine(
            TestEnvironment.RepositoryRoot,
            "src",
            "HelpDesk.NewWeb",
            "Components",
            "Pages",
            "Incidents",
            "IncidentRelationTargetPickerDialog.razor");
        var contents = File.ReadAllText(pickerPath);

        Assert.Contains("MultiSelection=\"true\"", contents, StringComparison.Ordinal);
        Assert.Contains("@bind-SelectedItems=\"selected\"", contents, StringComparison.Ordinal);
        Assert.Contains("SelectOnRowClick=\"false\"", contents, StringComparison.Ordinal);
        Assert.Contains("SelectionChangeable=\"true\"", contents, StringComparison.Ordinal);
        Assert.Contains("Comparer=\"IncidentIdentityComparer.Instance\"", contents, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedItemsChanged=", contents, StringComparison.Ordinal);
    }
}
