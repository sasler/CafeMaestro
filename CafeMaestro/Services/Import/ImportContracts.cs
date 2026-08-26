using CafeMaestro.Models;

namespace CafeMaestro.Services;

/// <summary>
/// The destination a CSV import writes into. Carried as a route parameter so Beans, Roast Log,
/// and Data &amp; Backups all reach the same flow with the right type already chosen.
/// </summary>
public enum ImportKind
{
    Beans,
    Roasts
}

/// <summary>
/// Presentation states of the shared import flow.
/// </summary>
public enum ImportStep
{
    SelectFile,
    MapColumns,
    Review,
    Result
}

/// <summary>
/// One mappable destination field, plus the hints used to auto-map a CSV header onto it.
/// </summary>
/// <param name="PropertyKey">Stable key used by the commit adapter.</param>
/// <param name="DisplayName">Human label shown in the mapping step.</param>
/// <param name="IsRequired">Required fields gate the Review step.</param>
/// <param name="Keywords">Partial matches; an exact keyword scores higher than a substring.</param>
/// <param name="ExactAliases">Normalized headers that mean this field even though they do not read like it.</param>
/// <param name="ContainsAliases">Normalized fragments that identify this field, including known misspellings.</param>
public sealed record ImportFieldDefinition(
    string PropertyKey,
    string DisplayName,
    bool IsRequired,
    IReadOnlyList<string> Keywords,
    IReadOnlyList<string>? ExactAliases = null,
    IReadOnlyList<string>? ContainsAliases = null);

/// <summary>
/// Copy that differs per import kind. Everything else in the flow is shared.
/// </summary>
public sealed record ImportKindDescriptor(
    ImportKind Kind,
    string TypeTitle,
    string TypeSummary,
    string ItemSingular,
    string ItemPlural,
    string FilePickerTitle,
    string DestinationActionLabel,
    string DestinationRoute);

/// <summary>
/// The verdict for a single CSV data row.
/// </summary>
public sealed record ImportRowOutcome(
    int RowNumber,
    bool IsAccepted,
    string Title,
    string Detail)
{
    public string ErrorText => IsAccepted ? string.Empty : Detail;
}

/// <summary>
/// A fully evaluated file: every row already judged, nothing written yet.
/// </summary>
public sealed class ImportPlan
{
    internal ImportPlan(
        ImportKind kind,
        IImportSession session,
        IReadOnlyList<ImportRowOutcome> accepted,
        IReadOnlyList<ImportRowOutcome> rejected)
    {
        Kind = kind;
        Session = session;
        AcceptedRows = accepted;
        RejectedRows = rejected;
    }

    public ImportKind Kind { get; }

    public IReadOnlyList<ImportRowOutcome> AcceptedRows { get; }

    public IReadOnlyList<ImportRowOutcome> RejectedRows { get; }

    public int TotalRowCount => AcceptedRows.Count + RejectedRows.Count;

    internal IImportSession Session { get; }
}

/// <summary>
/// The outcome of the single atomic mutation that commits a plan.
/// </summary>
public sealed record ImportCommitResult(
    bool Succeeded,
    int Imported,
    int Skipped,
    IReadOnlyList<string> Errors);

/// <summary>
/// Headers and data rows read from a user-selected CSV file. The source file is never modified.
/// </summary>
public sealed record ImportFileContent(
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Rows);

/// <summary>
/// Type-specific field definitions, row validation, duplicate policy, and commit behaviour.
/// </summary>
public interface IImportAdapter
{
    ImportKindDescriptor Descriptor { get; }

    IReadOnlyList<ImportFieldDefinition> Fields { get; }

    Task<IImportSession> CreateSessionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// One import attempt. Rows are judged first; accepted rows are appended in a single mutation.
/// </summary>
public interface IImportSession
{
    int AcceptedCount { get; }

    ImportRowOutcome Evaluate(
        int rowNumber,
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, string> mappings);

    /// <summary>
    /// Appends every accepted row. Runs inside <see cref="IAppDataService.UpdateAsync"/> so the
    /// whole import either lands together or not at all.
    /// </summary>
    void Commit(AppData appData);
}
