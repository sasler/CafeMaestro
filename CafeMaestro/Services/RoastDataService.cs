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
        private readonly IRoastLevelService _roastLevelService;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);
        private bool _isInitialized = false;
        private string _currentDataFilePath = string.Empty;

        // Property to get the current data file path
        public string DataFilePath
        {
            get => _appDataService.DataFilePath;
        }

        public RoastDataService(IAppDataService appDataService, IRoastLevelService roastLevelService)
        {
            _appDataService = appDataService;
            _roastLevelService = roastLevelService;
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

        private static void PrepareNewRoastForPersistence(RoastData roast) =>
            NewRoastDefaults.Apply(roast);

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

                return await _appDataService.TryUpdateAsync(appData =>
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
                    return await _appDataService.SaveAppDataAsync(appData);
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
    }
}
