using CommunityToolkit.Mvvm.ComponentModel;

namespace CafeMaestro.ViewModels;

internal static class ImportViewModelConstants
{
    public const string NoneOption = "-- None --";
}

public sealed partial class CsvImportColumnMapping : ObservableObject
{
    public CsvImportColumnMapping(string propertyKey, string displayName, bool isRequired = false)
    {
        PropertyKey = propertyKey;
        DisplayName = displayName;
        IsRequired = isRequired;
    }

    public string PropertyKey { get; }

    public string DisplayName { get; }

    public bool IsRequired { get; }

    [ObservableProperty]
    public partial string SelectedHeader { get; set; } = ImportViewModelConstants.NoneOption;
}

public sealed class CsvPreviewRow
{
    public CsvPreviewRow(string title, string detail)
    {
        Title = title;
        Detail = detail;
    }

    public string Title { get; }

    public string Detail { get; }
}

internal sealed record ImportFieldDefinition(
    string PropertyKey,
    string DisplayName,
    bool IsRequired,
    string[] Keywords);
