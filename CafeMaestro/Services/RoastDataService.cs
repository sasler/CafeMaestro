using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CafeMaestro.Models;

namespace CafeMaestro.Services
{
    public class RoastDataService : IRoastDataService
    {
        private readonly IAppDataService _appDataService;
        private readonly ICsvParserService _csvParserService;
        private readonly IRoastLevelService _roastLevelService;
        private readonly ICoolingNotificationService _coolingNotifications;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);
        private bool _isInitialized = false;
        private string _currentDataFilePath = string.Empty;

        // Property to get the current data file path
        public string DataFilePath
        {
            get => _appDataService.DataFilePath;
        }

        public RoastDataService(IAppDataService appDataService, ICsvParserService csvParserService, IRoastLevelService roastLevelService)
            : this(appDataService, csvParserService, roastLevelService, new NoOpCoolingNotificationService())
        {
        }

        public RoastDataService(
            IAppDataService appDataService,
            ICsvParserService csvParserService,
            IRoastLevelService roastLevelService,
            ICoolingNotificationService coolingNotifications)
        {
            _appDataService = appDataService;
            _csvParserService = csvParserService;
            _roastLevelService = roastLevelService;
            _coolingNotifications = coolingNotifications ?? throw new ArgumentNullException(nameof(coolingNotifications));
            _currentDataFilePath = _appDataService.DataFilePath;

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            // Subscribe to data file path changes
            _appDataService.DataFilePathChanged += OnDataFilePathChanged;
        }

        // Handle data file path changes
        private void OnDataFilePathChanged(object? sender, string newPath)
        {
            // When the path changes, we should reload data immediately
            // But don't do it in the event handler to avoid deadlocks
            // Instead, queue it on a background thread
            Task.Run(async () =>
            {
                try
                {
                    // Update stored path
                    _currentDataFilePath = newPath;

                    // Reset initialized flag to force reload with new path
                    _isInitialized = false;

                    // Reload data with new path
                    await _appDataService.ReloadDataAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error reloading data after path change in RoastDataService: {ex.Message}");
                }
            });
        }

        // Initialize from preferences - ensure this is called at startup
        public async Task InitializeFromPreferencesAsync(IPreferencesService preferencesService)
        {
            await _initLock.WaitAsync();

            try
            {
                if (_isInitialized)
                {
                    return;
                }

                // Force a reload of data
                await _appDataService.ReloadDataAsync();

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing RoastDataService from preferences: {ex.Message}");
            }
            finally
            {
                _initLock.Release();
            }
        }

        public async Task<bool> SaveRoastDataAsync(RoastData roastData)
        {
            try
            {
                if (HasInvalidFinalWeight(roastData))
                {
                    return false;
                }

                PrepareNewRoastForPersistence(roastData);

                // Before saving, determine and set the roast level name (only if final weight is known)
                if (roastData.HasFinalWeight)
                {
                    string roastLevelName = await _roastLevelService.GetRoastLevelNameAsync(roastData.WeightLossPercentage);
                    roastData.RoastLevelName = roastLevelName;
                }
                else
                {
                    roastData.RoastLevelName = "Pending";
                }

                // Load full app data
                var appData = await _appDataService.LoadAppDataAsync();

                // Add the new data
                appData.RoastLogs.Add(roastData);

                // Save updated app data
                bool result = await _appDataService.SaveAppDataAsync(appData);

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving roast data: {ex.Message}");
                return false;
            }
        }

        public async Task<List<RoastData>> LoadRoastDataAsync()
        {
            try
            {
                // Load full app data
                var appData = await _appDataService.LoadAppDataAsync();

                // Check and update RoastLevelName for all roast logs
                bool needsUpdate = false;

                foreach (var roastLog in appData.RoastLogs)
                {
                    if (string.IsNullOrEmpty(roastLog.RoastLevelName) && roastLog.HasFinalWeight)
                    {
                        // Use the RoastLevelService to get the correct level name
                        roastLog.RoastLevelName = await _roastLevelService.GetRoastLevelNameAsync(roastLog.WeightLossPercentage);
                        needsUpdate = true;
                    }
                    else if (string.IsNullOrEmpty(roastLog.RoastLevelName) && !roastLog.HasFinalWeight)
                    {
                        roastLog.RoastLevelName = "Pending";
                        needsUpdate = true;
                    }
                }

                // If we updated any roast level names, save the changes back
                if (needsUpdate)
                {
                    await _appDataService.SaveAppDataAsync(appData);
                }

                return appData.RoastLogs ?? new List<RoastData>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading roast data: {ex.Message}");
                return new List<RoastData>();
            }
        }

        public async Task<List<RoastData>> SearchRoastDataAsync(string beanType = "")
        {
            var allData = await LoadRoastDataAsync();

            if (string.IsNullOrWhiteSpace(beanType))
                return allData;

            return allData.FindAll(r => r.BeanType.Contains(beanType, StringComparison.OrdinalIgnoreCase));
        }

        public async Task ExportRoastLogAsync(
            Stream destination,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(destination);
            if (!destination.CanWrite)
            {
                throw new ArgumentException("The destination stream must be writable.", nameof(destination));
            }

            List<RoastData> allData = await LoadRoastDataAsync();
            await using var writer = new StreamWriter(
                destination,
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                1024,
                leaveOpen: true);
            await writer.WriteLineAsync(
                "Date,Bean Type,Temperature,Batch Weight,Final Weight,Weight Loss %,Roast Time,Roast Level,Notes");

            foreach (RoastData roast in allData)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string weightLoss = roast.HasFinalWeight
                    ? roast.WeightLossPercentage.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                    : "Pending";
                string line = string.Join(
                    ",",
                    roast.RoastDate.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                    EscapeCsv(roast.BeanType),
                    roast.Temperature.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    roast.BatchWeight.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    roast.FinalWeight.GetValueOrDefault().ToString(System.Globalization.CultureInfo.InvariantCulture),
                    weightLoss,
                    EscapeCsv(roast.FormattedTime),
                    EscapeCsv(roast.RoastLevelName),
                    EscapeCsv(roast.Notes));
                await writer.WriteLineAsync(line);
            }

            await writer.FlushAsync(cancellationToken);
        }

        private static string EscapeCsv(string? value)
        {
            string safeValue = value ?? string.Empty;
            return $"\"{safeValue.Replace("\"", "\"\"")}\"";
        }
        // Import roasts from CSV file
        public async Task<(int Success, int Failed, List<string> Errors)> ImportRoastsFromCsvAsync(
            string filePath,
            Dictionary<string, string> columnMapping)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    throw new FileNotFoundException("CSV file not found", filePath);
                }

                using var notificationSuspension = _appDataService.SuspendNotifications();

                // Read the CSV data
                var csvData = await _csvParserService.ReadCsvContentAsync(filePath, int.MaxValue);

                var errors = new List<string>();
                int successCount = 0;
                int failedCount = 0;

                // Load existing roasts
                var existingRoasts = await GetAllRoastsAsync();

                // Also track roasts added in this import session
                var importedRoasts = new List<RoastData>();

                // Process each row
                foreach (var row in csvData)
                {
                    try
                    {
                        var roast = new RoastData
                        {
                            Id = Guid.NewGuid(), // Always generate a new ID for imported records
                            RoastDate = DateTime.Now, // Default
                            Temperature = 235, // Default
                            BeanType = "",
                            BatchWeight = 0,
                            FinalWeight = 0,
                            RoastMinutes = 0,
                            RoastSeconds = 0,
                            Notes = ""
                        };

                        // Apply values based on mapping
                        foreach (var mapping in columnMapping)
                        {
                            string roastProperty = mapping.Key;
                            string csvColumn = mapping.Value;

                            if (string.IsNullOrEmpty(csvColumn) || !row.ContainsKey(csvColumn))
                            {
                                continue; // Skip if no mapping or column not found
                            }

                            string csvValue = row[csvColumn];

                            // Skip empty values
                            if (string.IsNullOrWhiteSpace(csvValue))
                            {
                                continue;
                            }

                            // Apply value based on property
                            switch (roastProperty)
                            {
                                case "RoastDate":
                                    try
                                    {
                                        // Try multiple date formats, including European format (dd/MM/yyyy)
                                        if (DateTime.TryParse(csvValue, out DateTime roastDate))
                                        {
                                            roast.RoastDate = roastDate;
                                        }
                                        else if (DateTime.TryParseExact(
                                            csvValue,
                                            new[] { "dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd" },
                                            System.Globalization.CultureInfo.InvariantCulture,
                                            System.Globalization.DateTimeStyles.None,
                                            out roastDate))
                                        {
                                            roast.RoastDate = roastDate;
                                        }
                                        else
                                        {
                                            System.Diagnostics.Debug.WriteLine($"Failed to parse date: {csvValue}");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Error parsing date '{csvValue}': {ex.Message}");
                                    }
                                    break;
                                // ... rest of the switch case handles other properties
                                case "BeanType":
                                    // Trim whitespace and double spaces
                                    roast.BeanType = csvValue.Trim().Replace("  ", " ");
                                    break;
                                case "Temperature":
                                    if (double.TryParse(csvValue, out double temp))
                                    {
                                        roast.Temperature = temp;
                                    }
                                    else if (int.TryParse(csvValue.Replace("°C", "").Replace("C", "").Trim(), out int tempInt))
                                    {
                                        roast.Temperature = tempInt;
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Failed to parse temperature: {csvValue}");
                                    }
                                    break;
                                case "RoastTime":
                                    try
                                    {
                                        // Parse time in format like "13:30" (mm:ss)
                                        string timeValue = csvValue.Trim();

                                        // Handle different time formats
                                        if (timeValue.Contains(":"))
                                        {
                                            // Manual parsing for mm:ss format (not hh:mm as TimeSpan.TryParse would interpret it)
                                            string[] parts = timeValue.Split(':');
                                            if (parts.Length == 2 && int.TryParse(parts[0], out int minutes) && int.TryParse(parts[1], out int seconds))
                                            {
                                                roast.RoastMinutes = minutes;
                                                roast.RoastSeconds = seconds;
                                            }
                                            else if (TimeSpan.TryParse(timeValue, out TimeSpan timeSpan))
                                            {
                                                // Interpret TimeSpan as mm:ss format rather than hh:mm
                                                // Convert hours from TimeSpan to minutes for our usage
                                                roast.RoastMinutes = timeSpan.Hours * 60 + timeSpan.Minutes;
                                                roast.RoastSeconds = timeSpan.Seconds;
                                            }
                                            else
                                            {
                                                System.Diagnostics.Debug.WriteLine($"Failed to parse time: {csvValue}");
                                            }
                                        }
                                        else if (int.TryParse(timeValue, out int totalSeconds))
                                        {
                                            // Handle pure seconds input
                                            roast.RoastMinutes = totalSeconds / 60;
                                            roast.RoastSeconds = totalSeconds % 60;
                                        }
                                        else
                                        {
                                            System.Diagnostics.Debug.WriteLine($"Failed to parse time: {csvValue}");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Error parsing time '{csvValue}': {ex.Message}");
                                    }
                                    break;
                                case "BatchWeight":
                                    if (double.TryParse(csvValue.Replace("g", "").Trim(), out double batchWeight))
                                    {
                                        roast.BatchWeight = batchWeight;
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Failed to parse batch weight: {csvValue}");
                                    }
                                    break;
                                case "FinalWeight":
                                    if (double.TryParse(csvValue.Replace("g", "").Trim(), out double finalWeight))
                                    {
                                        roast.FinalWeight = finalWeight;
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Failed to parse final weight: {csvValue}");
                                    }
                                    break;
                                case "WeightLoss":
                                    // Can't set WeightLossPercentage directly as it's a read-only property
                                    // Instead, we'll ensure FinalWeight and BatchWeight are set properly
                                    string cleanedValue = csvValue.Replace("%", "").Trim();
                                    if (double.TryParse(cleanedValue, out double lossPercentage) && roast.BatchWeight > 0)
                                    {
                                        // If batch weight is set, we can calculate the final weight to achieve this loss percentage
                                        double calculatedFinalWeight = roast.BatchWeight * (1 - (lossPercentage / 100.0));
                                        roast.FinalWeight = Math.Round(calculatedFinalWeight, 2);
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Failed to parse weight loss percentage or batch weight not set: {csvValue}");
                                    }
                                    break;
                                case "Notes":
                                    roast.Notes = csvValue.Trim();
                                    break;
                            }
                        }

                        // Validate required fields
                        if (string.IsNullOrWhiteSpace(roast.BeanType))
                        {
                            throw new ArgumentException("Coffee bean name is required");
                        }

                        // Add the roast - using our custom method that traces exactly what's happening
                        bool success = await AddRoastDirectAsync(roast);

                        if (success)
                        {
                            successCount++;
                            // Add to track list
                            importedRoasts.Add(roast);
                        }
                        else
                        {
                            failedCount++;
                            string error = $"Failed to add {roast.BeanType} roast from {roast.RoastDate.ToShortDateString()}";
                            errors.Add(error);
                            System.Diagnostics.Debug.WriteLine($"ERROR: {error}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        string error = $"Row error: {ex.Message}";
                        errors.Add(error);
                        System.Diagnostics.Debug.WriteLine($"ERROR: {error}");
                        System.Diagnostics.Debug.WriteLine($"Exception details: {ex}");
                    }
                }

                return (successCount, failedCount, errors);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error importing roasts from CSV: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Exception details: {ex}");
                throw;
            }
        }

        // Special version of AddRoastAsync that avoids event recursion
        private async Task<bool> AddRoastDirectAsync(RoastData roast)
        {
            try
            {
                if (HasInvalidFinalWeight(roast))
                {
                    return false;
                }

                PrepareNewRoastForPersistence(roast);

                // Make sure ID is set
                if (roast.Id == Guid.Empty)
                {
                    roast.Id = Guid.NewGuid();
                }

                // Load full app data - with detailed tracing
                var appData = await _appDataService.LoadAppDataAsync();

                // Initialize roast logs list if null
                if (appData.RoastLogs == null)
                {
                    appData.RoastLogs = new List<RoastData>();
                }

                // Add the new roast log
                appData.RoastLogs.Add(roast);

                // Save updated app data
                bool success = await _appDataService.SaveAppDataAsync(appData);

                return success;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TRACE ERROR adding roast log: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"TRACE ERROR stack trace: {ex.StackTrace}");
                return false;
            }
        }

        public async Task<int> RemoveDuplicatesAsync()
        {
            try
            {
                // Load full app data
                var appData = await _appDataService.LoadAppDataAsync();

                if (appData.RoastLogs == null || appData.RoastLogs.Count == 0)
                {
                    return 0;
                }

                // Keep track of IDs we've seen
                var seenIds = new HashSet<Guid>();

                // Keep track of content signatures we've seen for content-based deduplication
                var seenContentSignatures = new HashSet<string>();

                // Original count
                int originalCount = appData.RoastLogs.Count;

                // New list with duplicates removed
                var uniqueRoasts = new List<RoastData>();

                foreach (var roast in appData.RoastLogs)
                {
                    // Check for ID-based duplicates
                    if (seenIds.Contains(roast.Id))
                    {
                        continue;
                    }

                    // Create a content signature for content-based deduplication
                    string contentSignature = $"{roast.BeanType}|{roast.RoastDate:yyyy-MM-dd}|{roast.BatchWeight}|{roast.Temperature}|{roast.RoastMinutes}:{roast.RoastSeconds}";

                    // Check for content-based duplicates
                    if (seenContentSignatures.Contains(contentSignature))
                    {
                        continue;
                    }

                    // Add to our sets of seen items
                    seenIds.Add(roast.Id);
                    seenContentSignatures.Add(contentSignature);

                    // Keep this roast in our unique list
                    uniqueRoasts.Add(roast);
                }

                // Calculate how many duplicates were removed
                int removedCount = originalCount - uniqueRoasts.Count;

                if (removedCount > 0)
                {
                    // Update the app data with deduplicated list
                    appData.RoastLogs = uniqueRoasts;

                    // Save the updated data
                    await _appDataService.SaveAppDataAsync(appData);
                }

                return removedCount;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error removing duplicates: {ex.Message}");
                return 0;
            }
        }

        public async Task<List<RoastData>> GetAllRoastsAsync()
        {
            return await LoadRoastDataAsync();
        }

        public async Task<bool> AddRoastAsync(RoastData roast)
        {
            try
            {
                if (HasInvalidFinalWeight(roast))
                {
                    return false;
                }

                PrepareNewRoastForPersistence(roast);

                // Make sure ID is set
                if (roast.Id == Guid.Empty)
                {
                    roast.Id = Guid.NewGuid();
                }

                // Load full app data
                var appData = await _appDataService.LoadAppDataAsync();

                // Initialize roast logs list if null
                if (appData.RoastLogs == null)
                {
                    appData.RoastLogs = new List<RoastData>();
                }

                // Check if this roast already exists
                var existing = appData.RoastLogs.FirstOrDefault(r => r.Id == roast.Id);
                if (existing != null)
                {
                    // This is a duplicate, don't add it again
                    return true; // Return true to indicate "success" even though we didn't add it
                }

                // Add the new roast log
                appData.RoastLogs.Add(roast);

                // Save updated app data
                return await _appDataService.SaveAppDataAsync(appData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding roast log: {ex.Message}");
                return false;
            }
        }

        public async Task<List<RoastData>> GetAllRoastLogsAsync()
        {
            try
            {
                // Load full app data
                var appData = await _appDataService.LoadAppDataAsync();

                return appData.RoastLogs?.ToList() ?? new List<RoastData>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting all roast logs: {ex.Message}");
                return new List<RoastData>();
            }
        }

        private static void PrepareNewRoastForPersistence(RoastData roast)
        {
            roast.BeanDisplaySnapshot = string.IsNullOrWhiteSpace(roast.BeanDisplaySnapshot)
                ? roast.BeanType
                : roast.BeanDisplaySnapshot;
            if (!roast.DroppedAtUtc.HasValue && roast.RoastDate != default)
            {
                roast.DroppedAtUtc =
                    V1ToV2AppDataMigration.ConvertLegacyRoastDate(roast.RoastDate);
            }

            if (roast.FinalWeight > 0)
            {
                roast.CompletionStatus = RoastCompletionStatus.Complete;
                return;
            }

            if (roast.FinalWeight is null or 0)
            {
                roast.FinalWeight = null;
                roast.CompletionStatus = RoastCompletionStatus.AwaitingWeight;
                roast.CoolingDurationSeconds ??= 0;
            }
        }

        private static bool HasInvalidFinalWeight(RoastData roast) =>
            roast.FinalWeight is double finalWeight &&
            (!double.IsFinite(finalWeight) || finalWeight < 0);

        // Get specific roast log by ID
        public async Task<RoastData?> GetRoastLogByIdAsync(Guid id)
        {
            try
            {
                // Load full app data
                var appData = await _appDataService.LoadAppDataAsync();

                // Find the specific roast log
                var roastData = appData.RoastLogs?.FirstOrDefault(r => r.Id == id);

                return roastData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting roast log by ID: {ex.Message}");
                return null;
            }
        }

        // Update an existing roast log
        public async Task<bool> UpdateRoastLogAsync(RoastData updatedRoast)
        {
            try
            {
                if (HasInvalidFinalWeight(updatedRoast))
                {
                    return false;
                }

                // Before saving, determine and set the roast level name
                if (updatedRoast.HasFinalWeight)
                {
                    updatedRoast.RoastLevelName = await _roastLevelService.GetRoastLevelNameAsync(updatedRoast.WeightLossPercentage);
                }
                else
                {
                    updatedRoast.RoastLevelName = updatedRoast.CompletionStatus switch
                    {
                        RoastCompletionStatus.Unweighed => "Unweighed",
                        RoastCompletionStatus.Discarded => "Discarded",
                        _ => "Pending"
                    };
                }

                bool saved = await _appDataService.TryUpdateAsync(appData =>
                {
                    RoastData? existingRoast = appData.RoastLogs
                        .FirstOrDefault(roast => roast.Id == updatedRoast.Id);
                    if (existingRoast is null)
                    {
                        return false;
                    }

                    if (updatedRoast.BeanId.HasValue &&
                        updatedRoast.BeanId != existingRoast.BeanId)
                    {
                        existingRoast.BeanId = updatedRoast.BeanId;
                        existingRoast.BeanDisplaySnapshot = updatedRoast.BeanType;
                    }

                    existingRoast.BeanType = updatedRoast.BeanType;
                    existingRoast.Temperature = updatedRoast.Temperature;
                    existingRoast.BatchWeight = updatedRoast.BatchWeight;
                    existingRoast.FinalWeight = updatedRoast.HasFinalWeight
                        ? updatedRoast.FinalWeight
                        : null;
                    existingRoast.CompletionStatus = updatedRoast.HasFinalWeight
                        ? RoastCompletionStatus.Complete
                        : updatedRoast.CompletionStatus is RoastCompletionStatus.Unweighed or RoastCompletionStatus.Discarded
                            ? updatedRoast.CompletionStatus
                            : RoastCompletionStatus.AwaitingWeight;
                    if (!updatedRoast.HasFinalWeight)
                    {
                        if (!existingRoast.DroppedAtUtc.HasValue &&
                            existingRoast.RoastDate != default)
                        {
                            existingRoast.DroppedAtUtc =
                                V1ToV2AppDataMigration.ConvertLegacyRoastDate(existingRoast.RoastDate);
                        }

                        existingRoast.CoolingDurationSeconds ??= 0;
                    }
                    existingRoast.RoastMinutes = updatedRoast.RoastMinutes;
                    existingRoast.RoastSeconds = updatedRoast.RoastSeconds;
                    existingRoast.Notes = updatedRoast.Notes;
                    existingRoast.RoastLevelName = updatedRoast.RoastLevelName;
                    existingRoast.FirstCrackMinutes = updatedRoast.FirstCrackMinutes;
                    existingRoast.FirstCrackSeconds = updatedRoast.FirstCrackSeconds;
                    return true;
                });
                if (!saved)
                {
                    return false;
                }

                if (updatedRoast.CompletionStatus == RoastCompletionStatus.AwaitingWeight &&
                    updatedRoast.ReadyToWeighAtUtc is DateTimeOffset readyAt)
                {
                    try
                    {
                        await _coolingNotifications.ScheduleCoolingReadyAsync(
                            updatedRoast.Id,
                            readyAt,
                            updatedRoast.BeanDisplaySnapshot,
                            updatedRoast.BatchNumber);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Cooling reminder reschedule failed: {ex.Message}");
                    }
                }
                else
                {
                    await TryCancelCoolingReminderAsync(updatedRoast.Id);
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating roast log: {ex.Message}");
                return false;
            }
        }

        // Delete a roast log by ID
        public async Task<bool> DeleteRoastLogAsync(Guid id)
        {
            try
            {
                // Load full app data
                var appData = await _appDataService.LoadAppDataAsync();

                // Find the roast to delete
                var roastToRemove = appData.RoastLogs?.FirstOrDefault(r => r.Id == id);

                if (roastToRemove != null && appData.RoastLogs != null)
                {
                    // Remove the roast
                    appData.RoastLogs.Remove(roastToRemove);

                    // Save updated app data
                    bool saved = await _appDataService.SaveAppDataAsync(appData);
                    if (saved)
                    {
                        await TryCancelCoolingReminderAsync(id);
                    }
                    return saved;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting roast log: {ex.Message}");
                return false;
            }
        }

        // Get the most recent roast for a specific bean type
        public async Task<RoastData?> GetLastRoastForBeanTypeAsync(string beanType)
        {
            try
            {
                if (string.IsNullOrEmpty(beanType))
                    return null;

                // Load all roast logs
                var allRoasts = await GetAllRoastsAsync();

                // Find the most recent roast with the matching bean type
                var lastRoast = allRoasts
                    .Where(r => r.BeanType.Equals(beanType, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(r => r.RoastDate)
                    .FirstOrDefault();

                return lastRoast;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error finding previous roast: {ex.Message}");
                return null;
            }
        }

        private async Task TryCancelCoolingReminderAsync(Guid roastId)
        {
            try
            {
                await _coolingNotifications.CancelAsync(roastId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cooling reminder cancellation failed: {ex.Message}");
            }
        }
    }
}
