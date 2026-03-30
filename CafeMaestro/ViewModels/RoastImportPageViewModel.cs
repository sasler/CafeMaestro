using System.Collections.ObjectModel;
using System.ComponentModel;
using CafeMaestro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafeMaestro.ViewModels;

public partial class RoastImportPageViewModel : ObservableObject
{
    private readonly IRoastDataService _roastDataService;
    private readonly ICsvParserService _csvParserService;
    private readonly INavigationService _navigationService;
    private readonly IAlertService _alertService;
    private readonly List<Dictionary<string, string>> _rawPreviewRows = [];
    private readonly ImportFieldDefinition[] _fieldDefinitions =
    [
        new("RoastDate", "Date *", true, ["date", "roastdate"]),
        new("BeanType", "Coffee Bean *", true, ["coffee", "bean", "type"]),
        new("Temperature", "Temperature", false, ["temp", "temperature"]),
        new("RoastTime", "Time", false, ["time", "duration"]),
        new("BatchWeight", "Batch Weight", false, ["batch", "weight", "charge"]),
        new("FinalWeight", "Final Weight", false, ["final", "drop"]),
        new("WeightLoss", "Loss Percentage", false, ["loss", "%", "shrink"]),
        new("Notes", "Notes", false, ["note", "notes", "comment"])
    ];

    private Func<Task<string?>>? _pickFileAsync;
    private bool _isUpdatingMappings;
    private int _validPreviewRowCount;

    public RoastImportPageViewModel(
        IRoastDataService roastDataService,
        ICsvParserService csvParserService,
        INavigationService navigationService,
        IAlertService alertService)
    {
        _roastDataService = roastDataService ?? throw new ArgumentNullException(nameof(roastDataService));
        _csvParserService = csvParserService ?? throw new ArgumentNullException(nameof(csvParserService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));

        Headers = CreateHeaders([]);
        ColumnMappings = CreateColumnMappings();
        PreviewData = [];
        ImportStatus = "Select a CSV file to begin.";
    }

    public Func<Task<string?>>? PickFileAsync
    {
        get => _pickFileAsync;
        set => _pickFileAsync = value;
    }

    public bool HasHeaders => Headers.Count > 1;

    public bool HasSelectedFile => !string.IsNullOrWhiteSpace(FilePath);

    public bool CanImport => !IsImporting && HasSelectedFile && HasRequiredMappings() && _validPreviewRowCount > 0;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _headers = [];

    [ObservableProperty]
    private ObservableCollection<CsvPreviewRow> _previewData = [];

    [ObservableProperty]
    private ObservableCollection<CsvImportColumnMapping> _columnMappings = [];

    [ObservableProperty]
    private string _importStatus = string.Empty;

    [ObservableProperty]
    private bool _isImporting;

    partial void OnFilePathChanged(string value)
    {
        NotifyStateChanged();
    }

    partial void OnHeadersChanged(ObservableCollection<string> value)
    {
        NotifyStateChanged();
    }

    partial void OnPreviewDataChanged(ObservableCollection<CsvPreviewRow> value)
    {
        NotifyStateChanged();
    }

    partial void OnIsImportingChanged(bool value)
    {
        NotifyStateChanged();
    }

    partial void OnColumnMappingsChanged(ObservableCollection<CsvImportColumnMapping>? oldValue, ObservableCollection<CsvImportColumnMapping> newValue)
    {
        if (oldValue is not null)
        {
            foreach (var mapping in oldValue)
            {
                mapping.PropertyChanged -= HandleMappingPropertyChanged;
            }
        }

        foreach (var mapping in newValue)
        {
            mapping.PropertyChanged += HandleMappingPropertyChanged;
        }

        NotifyStateChanged();
    }

    [RelayCommand]
    private async Task BrowseAsync()
    {
        if (PickFileAsync is null || IsImporting)
        {
            return;
        }

        try
        {
            string? selectedFilePath = await PickFileAsync();

            if (string.IsNullOrWhiteSpace(selectedFilePath))
            {
                if (!HasSelectedFile)
                {
                    ImportStatus = "No file selected.";
                }

                return;
            }

            await LoadSelectedFileAsync(selectedFilePath);
        }
        catch (Exception ex)
        {
            ImportStatus = $"Failed to select a CSV file: {ex.Message}";
            await _alertService.ShowAlertAsync("File Selection Error", ImportStatus, "OK");
        }
    }

    [RelayCommand(CanExecute = nameof(CanImport))]
    private async Task ImportAsync()
    {
        if (!CanImport)
        {
            await _alertService.ShowAlertAsync(
                "Import Unavailable",
                "Select a CSV file and map Date and Coffee Bean before importing.",
                "OK");
            return;
        }

        try
        {
            IsImporting = true;
            ImportStatus = "Importing roast data...";

            var result = await _roastDataService.ImportRoastsFromCsvAsync(FilePath, GetSelectedMappings());
            string summary = BuildResultSummary(result.Success, result.Failed, result.Errors);
            ImportStatus = summary;

            await _alertService.ShowAlertAsync("Import Complete", summary, "OK");
            await _navigationService.GoBackAsync();
        }
        catch (Exception ex)
        {
            ImportStatus = $"Failed to import roast logs: {ex.Message}";
            await _alertService.ShowAlertAsync("Import Error", ImportStatus, "OK");
        }
        finally
        {
            IsImporting = false;
        }
    }

    [RelayCommand]
    private Task CancelAsync()
    {
        return _navigationService.GoBackAsync();
    }

    public async Task LoadSelectedFileAsync(string selectedFilePath)
    {
        if (string.IsNullOrWhiteSpace(selectedFilePath))
        {
            return;
        }

        try
        {
            IsImporting = true;
            ImportStatus = "Reading CSV file...";
            FilePath = selectedFilePath;
            _rawPreviewRows.Clear();
            _validPreviewRowCount = 0;
            PreviewData = [];
            Headers = CreateHeaders([]);
            ColumnMappings = CreateColumnMappings();

            List<string> csvHeaders = await _csvParserService.GetCsvHeadersAsync(selectedFilePath);

            if (csvHeaders.Count == 0)
            {
                ImportStatus = "No CSV headers were found in the selected file.";
                return;
            }

            Headers = CreateHeaders(csvHeaders);
            _rawPreviewRows.AddRange(await _csvParserService.ReadCsvContentAsync(selectedFilePath, 5));
            ColumnMappings = CreateColumnMappings();

            ApplyAutoMappings();
            RefreshPreview();
        }
        catch (Exception ex)
        {
            ImportStatus = $"Failed to read CSV data: {ex.Message}";
            PreviewData = [];
            _validPreviewRowCount = 0;
            await _alertService.ShowAlertAsync("CSV Error", ImportStatus, "OK");
        }
        finally
        {
            IsImporting = false;
        }
    }

    private void HandleMappingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isUpdatingMappings || e.PropertyName != nameof(CsvImportColumnMapping.SelectedHeader))
        {
            return;
        }

        RefreshPreview();
    }

    private ObservableCollection<string> CreateHeaders(IEnumerable<string> csvHeaders)
    {
        return new ObservableCollection<string>(
            [ImportViewModelConstants.NoneOption, .. csvHeaders.Where(header => !string.IsNullOrWhiteSpace(header))]);
    }

    private ObservableCollection<CsvImportColumnMapping> CreateColumnMappings()
    {
        return new ObservableCollection<CsvImportColumnMapping>(
            _fieldDefinitions.Select(field => new CsvImportColumnMapping(field.PropertyKey, field.DisplayName, field.IsRequired)));
    }

    private void ApplyAutoMappings()
    {
        _isUpdatingMappings = true;

        try
        {
            List<string> availableHeaders = Headers
                .Where(IsMapped)
                .ToList();

            foreach (var mapping in ColumnMappings)
            {
                ImportFieldDefinition definition = _fieldDefinitions.First(field => field.PropertyKey == mapping.PropertyKey);
                mapping.SelectedHeader = FindBestHeader(definition, availableHeaders) ?? ImportViewModelConstants.NoneOption;
            }
        }
        finally
        {
            _isUpdatingMappings = false;
        }
    }

    private string? FindBestHeader(ImportFieldDefinition definition, IReadOnlyCollection<string> availableHeaders)
    {
        string displayName = Normalize(definition.DisplayName.Replace("*", string.Empty, StringComparison.Ordinal).Trim());
        string propertyName = Normalize(definition.PropertyKey);

        return availableHeaders
            .Select(header => new
            {
                Header = header,
                Score = ScoreHeader(header, displayName, propertyName, definition.Keywords)
            })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Header, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => candidate.Header)
            .FirstOrDefault();
    }

    private static int ScoreHeader(string header, string displayName, string propertyName, IReadOnlyCollection<string> keywords)
    {
        string normalizedHeader = Normalize(header);
        int score = 0;

        if (normalizedHeader == displayName || normalizedHeader == propertyName)
        {
            score += 100;
        }

        if (normalizedHeader == "type" && propertyName == Normalize("BeanType"))
        {
            score += 90;
        }

        if (normalizedHeader == "weightg" && propertyName == Normalize("BatchWeight"))
        {
            score += 80;
        }

        foreach (string keyword in keywords)
        {
            string normalizedKeyword = Normalize(keyword);

            if (normalizedHeader == normalizedKeyword)
            {
                score += 50;
            }
            else if (normalizedHeader.Contains(normalizedKeyword, StringComparison.Ordinal))
            {
                score += 10;
            }
        }

        return score;
    }

    private void RefreshPreview()
    {
        if (!HasHeaders)
        {
            _validPreviewRowCount = 0;
            PreviewData = [];
            ImportStatus = "Select a CSV file to begin.";
            NotifyStateChanged();
            return;
        }

        if (_rawPreviewRows.Count == 0)
        {
            _validPreviewRowCount = 0;
            PreviewData = [];
            ImportStatus = "No data rows were found in the selected CSV file.";
            NotifyStateChanged();
            return;
        }

        Dictionary<string, string> mappings = GetSelectedMappings();
        PreviewData = new ObservableCollection<CsvPreviewRow>(BuildPreviewRows(mappings));

        if (!HasRequiredMappings())
        {
            _validPreviewRowCount = 0;
            ImportStatus = "Map Date and Coffee Bean to enable import.";
            NotifyStateChanged();
            return;
        }

        _validPreviewRowCount = _rawPreviewRows.Count(row =>
            !string.IsNullOrWhiteSpace(GetMappedValue(row, mappings, "RoastDate")) &&
            !string.IsNullOrWhiteSpace(GetMappedValue(row, mappings, "BeanType")));

        ImportStatus = _validPreviewRowCount > 0
            ? $"Ready to import {_validPreviewRowCount} roast log(s) from {_rawPreviewRows.Count} preview row(s)."
            : "No valid roast rows were found. Check the mappings and CSV values.";

        NotifyStateChanged();
    }

    private IEnumerable<CsvPreviewRow> BuildPreviewRows(IReadOnlyDictionary<string, string> mappings)
    {
        bool hasRequiredMappings = HasRequiredMappings();

        return _rawPreviewRows.Select((row, index) =>
        {
            if (!hasRequiredMappings)
            {
                string rawSummary = string.Join(
                    " | ",
                    row.Take(3).Select(item => $"{item.Key}: {item.Value}"));

                return new CsvPreviewRow($"Row {index + 1}", rawSummary);
            }

            string roastDate = GetMappedValue(row, mappings, "RoastDate");
            string beanType = GetMappedValue(row, mappings, "BeanType");
            string roastTime = GetMappedValue(row, mappings, "RoastTime");
            string batchWeight = GetMappedValue(row, mappings, "BatchWeight");
            string finalWeight = GetMappedValue(row, mappings, "FinalWeight");

            var details = new List<string>();

            if (!string.IsNullOrWhiteSpace(roastTime))
            {
                details.Add($"Time: {roastTime}");
            }

            if (!string.IsNullOrWhiteSpace(batchWeight))
            {
                details.Add($"Batch: {batchWeight}");
            }

            if (!string.IsNullOrWhiteSpace(finalWeight))
            {
                details.Add($"Final: {finalWeight}");
            }

            string title = string.IsNullOrWhiteSpace(roastDate) && string.IsNullOrWhiteSpace(beanType)
                ? $"Row {index + 1}: Missing required values"
                : $"Row {index + 1}: {beanType} on {roastDate}";

            return new CsvPreviewRow(title, details.Count > 0 ? string.Join(" | ", details) : "No optional values mapped.");
        });
    }

    private Dictionary<string, string> GetSelectedMappings()
    {
        return ColumnMappings
            .Where(mapping => IsMapped(mapping.SelectedHeader))
            .ToDictionary(mapping => mapping.PropertyKey, mapping => mapping.SelectedHeader);
    }

    private bool HasRequiredMappings()
    {
        return ColumnMappings
            .Where(mapping => mapping.IsRequired)
            .All(mapping => IsMapped(mapping.SelectedHeader));
    }

    private static bool IsMapped(string? header)
    {
        return !string.IsNullOrWhiteSpace(header) &&
               !string.Equals(header, ImportViewModelConstants.NoneOption, StringComparison.Ordinal);
    }

    private static string GetMappedValue(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, string> mappings,
        string propertyName)
    {
        if (mappings.TryGetValue(propertyName, out string? header) &&
            row.TryGetValue(header, out string? value))
        {
            return value ?? string.Empty;
        }

        return string.Empty;
    }

    private static string Normalize(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .ToArray())
            .ToLowerInvariant();
    }

    private static string BuildResultSummary(int success, int failed, IReadOnlyCollection<string> errors)
    {
        string summary = $"Imported {success} roast log(s). Failed: {failed}.";

        if (errors.Count > 0)
        {
            summary += $"{Environment.NewLine}{Environment.NewLine}Errors:{Environment.NewLine}- " +
                       string.Join($"{Environment.NewLine}- ", errors.Take(5));

            if (errors.Count > 5)
            {
                summary += $"{Environment.NewLine}...and {errors.Count - 5} more.";
            }
        }

        return summary;
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(HasHeaders));
        OnPropertyChanged(nameof(HasSelectedFile));
        OnPropertyChanged(nameof(CanImport));
        ImportCommand.NotifyCanExecuteChanged();
    }
}
