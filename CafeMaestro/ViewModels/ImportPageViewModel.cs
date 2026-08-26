using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using CafeMaestro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafeMaestro.ViewModels;

/// <summary>
/// The one guided CSV import flow: choose type and file, map columns, review every row, then
/// commit the accepted rows in a single atomic mutation.
/// </summary>
/// <remarks>
/// Beans, Roast Log, and Data &amp; Backups all navigate here with <see cref="ImportKind"/>
/// preselected, so the type cards sit beside file selection rather than adding a first step.
/// </remarks>
public partial class ImportPageViewModel : ObservableObject, IQueryAttributable
{
    /// <summary>Route parameter key carrying the preselected <see cref="ImportKind"/>.</summary>
    public const string KindParameter = "kind";

    private const int PreviewRowLimit = 5;
    private const int ResultErrorLimit = 5;

    private readonly IImportService _importService;
    private readonly INavigationService _navigationService;
    private readonly IAlertService _alertService;
    private readonly IUserFileService _userFileService;

    private readonly List<IReadOnlyDictionary<string, string>> _rows = [];
    private ImportPlan? _plan;
    private CancellationTokenSource? _operationCts;
    private string? _temporaryFilePath;
    private bool _isApplyingAutoMap;
    private bool _mappingsChangedByUser;
    private bool _isLeavingFlow;

    public ImportPageViewModel(
        IImportService importService,
        INavigationService navigationService,
        IAlertService alertService,
        IUserFileService userFileService)
    {
        _importService = importService ?? throw new ArgumentNullException(nameof(importService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
        _userFileService = userFileService ?? throw new ArgumentNullException(nameof(userFileService));

        RequiredMappings = [];
        OptionalMappings = [];
        Headers = [];
        PreviewRows = [];
        RejectedRows = [];
        ResultErrors = [];

        ApplyKind(ImportKind.Beans);
    }

    // ---------------------------------------------------------------- flow state

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelectFileStep))]
    [NotifyPropertyChangedFor(nameof(IsMapStep))]
    [NotifyPropertyChangedFor(nameof(IsReviewStep))]
    [NotifyPropertyChangedFor(nameof(IsResultStep))]
    [NotifyPropertyChangedFor(nameof(StepTitle))]
    public partial ImportStep Step { get; set; } = ImportStep.SelectFile;

    public bool IsSelectFileStep => Step == ImportStep.SelectFile;

    public bool IsMapStep => Step == ImportStep.MapColumns;

    public bool IsReviewStep => Step == ImportStep.Review;

    public bool IsResultStep => Step == ImportStep.Result;

    public string StepTitle => Step switch
    {
        ImportStep.MapColumns => "Map fields",
        ImportStep.Review => "Review import",
        ImportStep.Result => "Import result",
        _ => "Import"
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBeansKind))]
    [NotifyPropertyChangedFor(nameof(IsRoastsKind))]
    public partial ImportKind Kind { get; set; } = ImportKind.Beans;

    public bool IsBeansKind => Kind == ImportKind.Beans;

    public bool IsRoastsKind => Kind == ImportKind.Roasts;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    /// <summary>Blocking, file-level problem such as an unreadable CSV or one with no headers.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFileError))]
    public partial string FileErrorMessage { get; set; } = string.Empty;

    public bool HasFileError => !string.IsNullOrEmpty(FileErrorMessage);

    // ---------------------------------------------------------------- file

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFile))]
    [NotifyPropertyChangedFor(nameof(HasNoFile))]
    public partial string FileDisplayName { get; set; } = string.Empty;

    public string FilePath { get; private set; } = string.Empty;

    public bool HasFile => !string.IsNullOrEmpty(FileDisplayName);

    public bool HasNoFile => !HasFile;

    public ObservableCollection<string> Headers { get; }

    // ---------------------------------------------------------------- mapping

    public ObservableCollection<ImportColumnMapping> RequiredMappings { get; }

    public ObservableCollection<ImportColumnMapping> OptionalMappings { get; }

    [ObservableProperty]
    public partial bool IsOptionalExpanded { get; set; }

    /// <summary>
    /// Phone keeps each mapping stacked (field above column); at tablet width the pair sits on
    /// one line. Driven by the page's size, never by a converter that queries layout.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MappingPickerRow))]
    [NotifyPropertyChangedFor(nameof(MappingPickerColumn))]
    [NotifyPropertyChangedFor(nameof(MappingLabelColumnSpan))]
    public partial bool IsWideLayout { get; set; }

    public int MappingPickerRow => IsWideLayout ? 0 : 1;

    public int MappingPickerColumn => IsWideLayout ? 1 : 0;

    public int MappingLabelColumnSpan => IsWideLayout ? 1 : 2;

    public void SetWideLayout(bool isWide) => IsWideLayout = isWide;

    [ObservableProperty]
    public partial int AutoMappedFieldCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMissingRequiredMappings))]
    public partial int MissingRequiredCount { get; set; }

    public bool HasMissingRequiredMappings => MissingRequiredCount > 0;

    public string AutoMapSummary => AutoMappedFieldCount == 1
        ? "1 field mapped automatically"
        : $"{AutoMappedFieldCount} fields mapped automatically";

    public string MissingRequiredSummary => MissingRequiredCount == 1
        ? "1 required field still needs a column"
        : $"{MissingRequiredCount} required fields still need a column";

    public string OptionalSummary => OptionalMappings.Count == 1
        ? "1 optional field"
        : $"{OptionalMappings.Count} optional fields";

    // ---------------------------------------------------------------- review

    public ObservableCollection<ImportRowPreview> PreviewRows { get; }

    public ObservableCollection<ImportRowPreview> RejectedRows { get; }

    [ObservableProperty]
    public partial int ValidRowCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRejectedRows))]
    public partial int InvalidRowCount { get; set; }

    [ObservableProperty]
    public partial int TotalRowCount { get; set; }

    public bool HasRejectedRows => InvalidRowCount > 0;

    [ObservableProperty]
    public partial bool IsRejectedExpanded { get; set; }

    /// <summary>States exactly what the primary action will write, per the import surface spec.</summary>
    public string ImportActionLabel => ValidRowCount == 1
        ? $"IMPORT 1 VALID {Descriptor.ItemSingular.ToUpperInvariant()}"
        : $"IMPORT {ValidRowCount} VALID {Descriptor.ItemPlural.ToUpperInvariant()}";

    // ---------------------------------------------------------------- result

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImportFailed))]
    public partial bool ImportSucceeded { get; set; }

    public bool ImportFailed => !ImportSucceeded;

    [ObservableProperty]
    public partial int ImportedCount { get; set; }

    [ObservableProperty]
    public partial int SkippedCount { get; set; }

    [ObservableProperty]
    public partial string ResultHeadline { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ResultDetail { get; set; } = string.Empty;

    public ObservableCollection<string> ResultErrors { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHiddenErrors))]
    public partial int HiddenErrorCount { get; set; }

    public bool HasHiddenErrors => HiddenErrorCount > 0;

    // ---------------------------------------------------------------- descriptors

    public ImportKindDescriptor Descriptor { get; private set; } = null!;

    public string BeansTypeSummary => _importService.Describe(ImportKind.Beans).TypeSummary;

    public string RoastsTypeSummary => _importService.Describe(ImportKind.Roasts).TypeSummary;

    public string DestinationActionLabel => Descriptor.DestinationActionLabel;

    // ---------------------------------------------------------------- navigation entry

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!query.TryGetValue(KindParameter, out object? value))
        {
            return;
        }

        ImportKind? kind = value switch
        {
            ImportKind typed => typed,
            string text when Enum.TryParse(text, ignoreCase: true, out ImportKind parsed) => parsed,
            _ => null
        };

        if (kind is not null && kind != Kind)
        {
            ApplyKind(kind.Value);
            ResetFlow();
        }
    }

    // ---------------------------------------------------------------- commands

    [RelayCommand]
    private void SelectKind(ImportKind kind)
    {
        if (IsBusy || kind == Kind)
        {
            return;
        }

        ApplyKind(kind);

        // The chosen file stays selected: only the destination rules changed.
        if (Headers.Count > 0)
        {
            ApplyAutoMappings();
        }

        _plan = null;
        Step = ImportStep.SelectFile;
    }

    [RelayCommand]
    private async Task BrowseAsync()
    {
        if (IsBusy)
        {
            return;
        }

        CancellationToken cancellationToken = BeginOperation();

        try
        {
            IsBusy = true;
            StatusMessage = "Waiting for a file…";

            UserFileSelection? selection = await _userFileService.PickFileAsync(
                UserFileType.Csv,
                Descriptor.FilePickerTitle,
                cancellationToken);

            if (selection is null)
            {
                StatusMessage = HasFile ? string.Empty : "No file selected.";
                return;
            }

            _userFileService.DeleteTemporaryFile(_temporaryFilePath);
            _temporaryFilePath = selection.LocalPath;
            await LoadFileAsync(selection, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "File selection cancelled.";
        }
        catch (Exception ex)
        {
            ClearFile();
            FileErrorMessage = $"CafeMaestro could not read that file. {ex.Message}";
            await _alertService.ShowAlertAsync("File error", FileErrorMessage, "OK");
        }
        finally
        {
            IsBusy = false;
            EndOperation();
        }
    }

    [RelayCommand(CanExecute = nameof(CanContinueToMapping))]
    private void ContinueToMapping()
    {
        FileErrorMessage = string.Empty;
        Step = ImportStep.MapColumns;
    }

    private bool CanContinueToMapping() => !IsBusy && HasFile && Headers.Count > 1 && _rows.Count > 0;

    [RelayCommand]
    private void BackToFile()
    {
        Step = ImportStep.SelectFile;
    }

    [RelayCommand]
    private void AutoMap()
    {
        if (Headers.Count == 0)
        {
            return;
        }

        ApplyAutoMappings();
        _mappingsChangedByUser = false;
    }

    [RelayCommand]
    private void ToggleOptionalFields() => IsOptionalExpanded = !IsOptionalExpanded;

    [RelayCommand]
    private void ToggleRejectedRows() => IsRejectedExpanded = !IsRejectedExpanded;

    [RelayCommand(CanExecute = nameof(CanReview))]
    private async Task ReviewAsync()
    {
        CancellationToken cancellationToken = BeginOperation();

        try
        {
            IsBusy = true;
            StatusMessage = "Checking every row…";

            _plan = await _importService.BuildPlanAsync(Kind, _rows, GetSelectedMappings(), cancellationToken);
            ApplyPlan(_plan);
            Step = ImportStep.Review;
            StatusMessage = string.Empty;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Review cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = string.Empty;
            await _alertService.ShowAlertAsync(
                "Review failed",
                $"CafeMaestro could not read the rows in this file. {ex.Message}",
                "OK");
        }
        finally
        {
            IsBusy = false;
            EndOperation();
        }
    }

    private bool CanReview() => !IsBusy && HasFile && !HasMissingRequiredMappings;

    [RelayCommand]
    private void BackToMapping()
    {
        Step = ImportStep.MapColumns;
    }

    [RelayCommand(CanExecute = nameof(CanImport))]
    private Task ImportAsync() => CommitAsync();

    private bool CanImport() => !IsBusy && _plan is not null && ValidRowCount > 0;

    [RelayCommand(CanExecute = nameof(CanRetry))]
    private Task RetryAsync() => CommitAsync();

    private bool CanRetry() => !IsBusy && _plan is not null && !ImportSucceeded && ValidRowCount > 0;

    [RelayCommand]
    private async Task ChooseAnotherFileAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (!await ConfirmAbandonMappingsAsync())
        {
            return;
        }

        ClearFile();
        Step = ImportStep.SelectFile;
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (IsBusy)
        {
            // The commit owns the flow while it runs; cancelling here would strand a half-reviewed plan.
            return;
        }

        if (!await ConfirmAbandonMappingsAsync())
        {
            return;
        }

        _isLeavingFlow = true;
        await _navigationService.GoBackAsync();
    }

    [RelayCommand]
    private async Task OpenDestinationAsync()
    {
        _isLeavingFlow = true;
        await _navigationService.GoToAsync(Descriptor.DestinationRoute);
    }

    [RelayCommand(CanExecute = nameof(CanShareReport))]
    private async Task ShareReportAsync()
    {
        try
        {
            byte[] report = Encoding.UTF8.GetBytes(BuildReport());
            using var stream = new MemoryStream(report);
            DocumentSaveResult result = await _userFileService.SaveFileAsync(
                $"CafeMaestro_ImportReport_{DateTime.Now:yyyy-MM-dd_HHmm}.txt",
                "text/plain",
                stream);

            if (!result.IsSuccessful && result.Exception is not null)
            {
                throw result.Exception;
            }
        }
        catch (Exception ex)
        {
            await _alertService.ShowAlertAsync(
                "Share report",
                $"CafeMaestro could not save the import report. {ex.Message}",
                "OK");
        }
    }

    private bool CanShareReport() => IsResultStep;

    // ---------------------------------------------------------------- lifecycle

    /// <summary>
    /// Releases the read-only working copy of the source file once the user has left the flow.
    /// A transient disappearance keeps the file so mappings survive.
    /// </summary>
    public void OnDisappearing()
    {
        if (!_isLeavingFlow)
        {
            return;
        }

        _operationCts?.Cancel();

        _userFileService.DeleteTemporaryFile(_temporaryFilePath);
        _temporaryFilePath = null;
    }

    /// <summary>True while a commit is running, when system back must not leave the page.</summary>
    public bool IsNavigationGuarded => IsBusy;

    // ---------------------------------------------------------------- internals

    private async Task CommitAsync()
    {
        if (_plan is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = $"Importing {ValidRowCount} {(ValidRowCount == 1 ? Descriptor.ItemSingular : Descriptor.ItemPlural)}…";

            // Reading and reviewing are cancellable; the commit itself is not. It is one short
            // atomic mutation, and abandoning it mid-write would risk the very thing atomicity
            // exists to prevent.
            ImportCommitResult result = await _importService.CommitAsync(_plan, CancellationToken.None);
            ApplyResult(result);
            Step = ImportStep.Result;
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            ApplyResult(new ImportCommitResult(
                false,
                0,
                InvalidRowCount,
                [$"The import could not be saved. {ex.Message}"]));
            Step = ImportStep.Result;
        }
        finally
        {
            IsBusy = false;
            EndOperation();
        }
    }

    private async Task LoadFileAsync(UserFileSelection selection, CancellationToken cancellationToken)
    {
        StatusMessage = "Reading headers…";
        FileErrorMessage = string.Empty;
        FileDisplayName = selection.DisplayName;
        FilePath = selection.LocalPath;
        _rows.Clear();
        _plan = null;
        _mappingsChangedByUser = false;
        PreviewRows.Clear();
        RejectedRows.Clear();
        ResultErrors.Clear();
        ValidRowCount = 0;
        InvalidRowCount = 0;
        TotalRowCount = 0;
        ImportedCount = 0;
        SkippedCount = 0;
        ImportSucceeded = false;

        ImportFileContent content = await _importService.ReadFileAsync(selection.LocalPath, cancellationToken);

        if (content.Headers.Count == 0)
        {
            SetHeaders([]);
            FileErrorMessage = "That file has no header row. Choose a CSV whose first row names the columns.";
            StatusMessage = string.Empty;
            return;
        }

        if (content.Rows.Count == 0)
        {
            SetHeaders(content.Headers);
            FileErrorMessage = "That file has headers but no data rows. Choose a CSV that contains records.";
            StatusMessage = string.Empty;
            return;
        }

        SetHeaders(content.Headers);
        _rows.AddRange(content.Rows);
        TotalRowCount = _rows.Count;
        ApplyAutoMappings();
        StatusMessage = _rows.Count == 1 ? "1 row found." : $"{_rows.Count} rows found.";
        Step = ImportStep.MapColumns;
    }

    private void ApplyKind(ImportKind kind)
    {
        Kind = kind;
        Descriptor = _importService.Describe(kind);

        DetachMappings(RequiredMappings);
        DetachMappings(OptionalMappings);
        RequiredMappings.Clear();
        OptionalMappings.Clear();

        foreach (ImportFieldDefinition field in _importService.GetFields(kind))
        {
            var mapping = new ImportColumnMapping(field);
            mapping.PropertyChanged += HandleMappingChanged;

            if (field.IsRequired)
            {
                RequiredMappings.Add(mapping);
            }
            else
            {
                OptionalMappings.Add(mapping);
            }
        }

        MissingRequiredCount = RequiredMappings.Count;
        AutoMappedFieldCount = 0;
        OnPropertyChanged(nameof(Descriptor));
        OnPropertyChanged(nameof(DestinationActionLabel));
        OnPropertyChanged(nameof(OptionalSummary));
        OnPropertyChanged(nameof(ImportActionLabel));
        NotifyMappingSummaries();
    }

    private void ResetFlow()
    {
        ClearFile();
        Step = ImportStep.SelectFile;
    }

    private void ClearFile()
    {
        _userFileService.DeleteTemporaryFile(_temporaryFilePath);
        _temporaryFilePath = null;
        FileDisplayName = string.Empty;
        FilePath = string.Empty;
        FileErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        _rows.Clear();
        _plan = null;
        _mappingsChangedByUser = false;
        SetHeaders([]);
        PreviewRows.Clear();
        RejectedRows.Clear();
        ResultErrors.Clear();
        ValidRowCount = 0;
        InvalidRowCount = 0;
        TotalRowCount = 0;
        ImportedCount = 0;
        SkippedCount = 0;
        ImportSucceeded = false;
        ApplyKind(Kind);
        NotifyCommandStates();
    }

    private void SetHeaders(IReadOnlyList<string> headers)
    {
        Headers.Clear();
        Headers.Add(ImportHeaderMatcher.NoneOption);

        foreach (string header in headers)
        {
            Headers.Add(header);
        }
    }

    private void ApplyAutoMappings()
    {
        IReadOnlyDictionary<string, string> suggestions =
            _importService.SuggestMappings(Kind, Headers.Where(ImportHeaderMatcher.IsSelectableHeader));

        _isApplyingAutoMap = true;

        try
        {
            foreach (ImportColumnMapping mapping in EnumerateMappings())
            {
                mapping.SelectedHeader = suggestions.TryGetValue(mapping.PropertyKey, out string? header)
                    ? header
                    : ImportHeaderMatcher.NoneOption;
            }
        }
        finally
        {
            _isApplyingAutoMap = false;
        }

        RefreshMappingCounts();
        IsOptionalExpanded = false;
    }

    private void HandleMappingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ImportColumnMapping.SelectedHeader))
        {
            return;
        }

        if (!_isApplyingAutoMap)
        {
            _mappingsChangedByUser = true;
            _plan = null;
        }

        RefreshMappingCounts();
    }

    private void RefreshMappingCounts()
    {
        AutoMappedFieldCount = EnumerateMappings().Count(mapping => mapping.IsMapped);
        MissingRequiredCount = RequiredMappings.Count(mapping => !mapping.IsMapped);
        NotifyMappingSummaries();
        NotifyCommandStates();
    }

    private void NotifyMappingSummaries()
    {
        OnPropertyChanged(nameof(AutoMapSummary));
        OnPropertyChanged(nameof(MissingRequiredSummary));
    }

    private IEnumerable<ImportColumnMapping> EnumerateMappings() =>
        RequiredMappings.Concat(OptionalMappings);

    private Dictionary<string, string> GetSelectedMappings() =>
        EnumerateMappings()
            .Where(mapping => mapping.IsMapped)
            .ToDictionary(mapping => mapping.PropertyKey, mapping => mapping.SelectedHeader, StringComparer.Ordinal);

    private void ApplyPlan(ImportPlan plan)
    {
        PreviewRows.Clear();
        RejectedRows.Clear();

        foreach (ImportRowOutcome outcome in plan.AcceptedRows.Take(PreviewRowLimit))
        {
            PreviewRows.Add(new ImportRowPreview(outcome));
        }

        foreach (ImportRowOutcome outcome in plan.RejectedRows)
        {
            RejectedRows.Add(new ImportRowPreview(outcome));
        }

        ValidRowCount = plan.AcceptedRows.Count;
        InvalidRowCount = plan.RejectedRows.Count;
        TotalRowCount = plan.TotalRowCount;
        IsRejectedExpanded = false;
        OnPropertyChanged(nameof(ImportActionLabel));
        NotifyCommandStates();
    }

    private void ApplyResult(ImportCommitResult result)
    {
        ImportSucceeded = result.Succeeded;
        ImportedCount = result.Imported;
        SkippedCount = result.Skipped;

        ResultErrors.Clear();

        foreach (string error in result.Errors.Take(ResultErrorLimit))
        {
            ResultErrors.Add(error);
        }

        HiddenErrorCount = Math.Max(0, result.Errors.Count - ResultErrorLimit);

        string noun = result.Imported == 1 ? Descriptor.ItemSingular : Descriptor.ItemPlural;

        if (!result.Succeeded)
        {
            ResultHeadline = "Nothing was imported";
            ResultDetail = ValidRowCount > 0
                ? "Your file, mappings, and review are still here. Try the import again."
                : $"No row in this file could be added as a {Descriptor.ItemSingular}.";
        }
        else if (result.Skipped > 0)
        {
            ResultHeadline = $"Imported {result.Imported} {noun}";
            ResultDetail = $"{result.Skipped} row(s) were skipped and are listed below.";
        }
        else
        {
            ResultHeadline = $"Imported {result.Imported} {noun}";
            ResultDetail = "Every row in the file was imported.";
        }

        NotifyCommandStates();
    }

    private string BuildReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"CafeMaestro import report — {Descriptor.TypeTitle}");
        builder.AppendLine($"File: {FileDisplayName}");
        builder.AppendLine($"Imported: {ImportedCount}");
        builder.AppendLine($"Skipped: {SkippedCount}");
        builder.AppendLine($"Rows read: {TotalRowCount}");

        if (RejectedRows.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Skipped rows:");

            foreach (ImportRowPreview row in RejectedRows)
            {
                builder.AppendLine($"- {row.Title}: {row.Detail}");
            }
        }

        return builder.ToString();
    }

    private async Task<bool> ConfirmAbandonMappingsAsync()
    {
        // Selecting a file alone creates nothing, so only edited mappings are worth confirming.
        if (!_mappingsChangedByUser || IsResultStep)
        {
            return true;
        }

        return await _alertService.ShowConfirmationAsync(
            "Discard mapping?",
            "Your column mapping will be lost. The file itself is never changed.",
            "Discard",
            "Keep editing");
    }

    private CancellationToken BeginOperation()
    {
        _operationCts?.Cancel();
        _operationCts?.Dispose();
        _operationCts = new CancellationTokenSource();
        return _operationCts.Token;
    }

    private void EndOperation()
    {
        _operationCts?.Dispose();
        _operationCts = null;
    }

    private void DetachMappings(IEnumerable<ImportColumnMapping> mappings)
    {
        foreach (ImportColumnMapping mapping in mappings)
        {
            mapping.PropertyChanged -= HandleMappingChanged;
        }
    }

    partial void OnIsBusyChanged(bool value) => NotifyCommandStates();

    partial void OnStepChanged(ImportStep value) => NotifyCommandStates();

    private void NotifyCommandStates()
    {
        ContinueToMappingCommand.NotifyCanExecuteChanged();
        ReviewCommand.NotifyCanExecuteChanged();
        ImportCommand.NotifyCanExecuteChanged();
        RetryCommand.NotifyCanExecuteChanged();
        ShareReportCommand.NotifyCanExecuteChanged();
    }
}
