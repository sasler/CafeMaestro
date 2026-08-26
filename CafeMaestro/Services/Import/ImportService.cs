namespace CafeMaestro.Services;

/// <inheritdoc />
public sealed class ImportService : IImportService
{
    private readonly ICsvParserService _csvParserService;
    private readonly IAppDataService _appDataService;
    private readonly IReadOnlyDictionary<ImportKind, IImportAdapter> _adapters;

    public ImportService(
        ICsvParserService csvParserService,
        IAppDataService appDataService,
        IEnumerable<IImportAdapter> adapters)
    {
        _csvParserService = csvParserService ?? throw new ArgumentNullException(nameof(csvParserService));
        _appDataService = appDataService ?? throw new ArgumentNullException(nameof(appDataService));
        ArgumentNullException.ThrowIfNull(adapters);
        _adapters = adapters.ToDictionary(adapter => adapter.Descriptor.Kind);
    }

    public ImportKindDescriptor Describe(ImportKind kind) => GetAdapter(kind).Descriptor;

    public IReadOnlyList<ImportFieldDefinition> GetFields(ImportKind kind) => GetAdapter(kind).Fields;

    public IReadOnlyDictionary<string, string> SuggestMappings(ImportKind kind, IEnumerable<string> headers) =>
        ImportHeaderMatcher.SuggestMappings(GetAdapter(kind).Fields, headers);

    public async Task<ImportFileContent> ReadFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        cancellationToken.ThrowIfCancellationRequested();

        List<string> headers = await _csvParserService.GetCsvHeadersAsync(filePath);
        cancellationToken.ThrowIfCancellationRequested();

        if (headers.Count == 0)
        {
            return new ImportFileContent([], []);
        }

        List<Dictionary<string, string>> rows =
            await _csvParserService.ReadCsvContentAsync(filePath, int.MaxValue);
        cancellationToken.ThrowIfCancellationRequested();

        return new ImportFileContent(
            headers.Where(ImportHeaderMatcher.IsSelectableHeader).ToList(),
            rows.Cast<IReadOnlyDictionary<string, string>>().ToList());
    }

    public async Task<ImportPlan> BuildPlanAsync(
        ImportKind kind,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows,
        IReadOnlyDictionary<string, string> mappings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(mappings);

        IImportAdapter adapter = GetAdapter(kind);
        IImportSession session = await adapter.CreateSessionAsync(cancellationToken);

        var accepted = new List<ImportRowOutcome>();
        var rejected = new List<ImportRowOutcome>();

        for (int index = 0; index < rows.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImportRowOutcome outcome = session.Evaluate(index + 1, rows[index], mappings);

            if (outcome.IsAccepted)
            {
                accepted.Add(outcome);
            }
            else
            {
                rejected.Add(outcome);
            }
        }

        return new ImportPlan(kind, session, accepted, rejected);
    }

    public async Task<ImportCommitResult> CommitAsync(
        ImportPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        List<string> rejectionErrors = plan.RejectedRows
            .Select(row => $"{row.Title}: {row.ErrorText}")
            .ToList();

        if (plan.AcceptedRows.Count == 0)
        {
            return new ImportCommitResult(false, 0, plan.RejectedRows.Count, rejectionErrors);
        }

        bool committed = await _appDataService.UpdateAsync(
            appData => plan.Session.Commit(appData),
            cancellationToken);

        if (!committed)
        {
            // Nothing was written: the whole accepted set is still pending, so the flow can retry
            // from the reviewed plan without asking for the file again.
            return new ImportCommitResult(
                false,
                0,
                plan.RejectedRows.Count,
                [.. rejectionErrors.Prepend("The import could not be saved. No records were changed.")]);
        }

        return new ImportCommitResult(
            true,
            plan.AcceptedRows.Count,
            plan.RejectedRows.Count,
            rejectionErrors);
    }

    private IImportAdapter GetAdapter(ImportKind kind)
    {
        if (_adapters.TryGetValue(kind, out IImportAdapter? adapter))
        {
            return adapter;
        }

        throw new ArgumentOutOfRangeException(nameof(kind), kind, "No import adapter is registered for this kind.");
    }
}
