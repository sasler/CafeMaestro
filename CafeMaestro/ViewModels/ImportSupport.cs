using CafeMaestro.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CafeMaestro.ViewModels;

/// <summary>
/// One destination field bound to a CSV column. Required fields that stay unmapped are what the
/// mapping step expands and highlights.
/// </summary>
public sealed partial class ImportColumnMapping : ObservableObject
{
    public ImportColumnMapping(ImportFieldDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        PropertyKey = definition.PropertyKey;
        DisplayName = definition.IsRequired ? $"{definition.DisplayName} *" : definition.DisplayName;
        FieldName = definition.DisplayName;
        IsRequired = definition.IsRequired;
    }

    public string PropertyKey { get; }

    /// <summary>Label including the required marker.</summary>
    public string DisplayName { get; }

    /// <summary>Label without the required marker, for prose such as the missing-mapping hint.</summary>
    public string FieldName { get; }

    public bool IsRequired { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMapped))]
    [NotifyPropertyChangedFor(nameof(NeedsAttention))]
    public partial string SelectedHeader { get; set; } = ImportHeaderMatcher.NoneOption;

    public bool IsMapped => ImportHeaderMatcher.IsSelectableHeader(SelectedHeader);

    public bool NeedsAttention => IsRequired && !IsMapped;
}

/// <summary>
/// A row as it appears in Review or in the result report.
/// </summary>
public sealed class ImportRowPreview
{
    public ImportRowPreview(ImportRowOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        RowNumber = outcome.RowNumber;
        Title = outcome.Title;
        Detail = outcome.Detail;
        IsRejected = !outcome.IsAccepted;
    }

    public int RowNumber { get; }

    public string Title { get; }

    public string Detail { get; }

    public bool IsRejected { get; }
}
