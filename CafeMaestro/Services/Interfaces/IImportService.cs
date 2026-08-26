namespace CafeMaestro.Services;

/// <summary>
/// Orchestrates the shared CSV import flow: read a read-only source, evaluate every row against
/// the destination's rules, then commit the accepted rows in one atomic app-data mutation.
/// </summary>
public interface IImportService
{
    ImportKindDescriptor Describe(ImportKind kind);

    IReadOnlyList<ImportFieldDefinition> GetFields(ImportKind kind);

    /// <summary>Best-guess column mapping. Never decides on the user's behalf: review still gates the write.</summary>
    IReadOnlyDictionary<string, string> SuggestMappings(ImportKind kind, IEnumerable<string> headers);

    /// <summary>Reads headers and every data row. The source file is only ever read.</summary>
    Task<ImportFileContent> ReadFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>Judges every row. Nothing is written, so this is safe to repeat after a mapping change.</summary>
    Task<ImportPlan> BuildPlanAsync(
        ImportKind kind,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows,
        IReadOnlyDictionary<string, string> mappings,
        CancellationToken cancellationToken = default);

    /// <summary>Appends every accepted row in a single mutation that raises one data notification.</summary>
    Task<ImportCommitResult> CommitAsync(ImportPlan plan, CancellationToken cancellationToken = default);
}
