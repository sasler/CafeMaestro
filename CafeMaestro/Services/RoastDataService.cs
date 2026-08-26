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
    public class RoastDataService : IRoastDataService, IDisposable
    {
        private readonly IAppDataService _appDataService;
        private readonly IRoastLevelService _roastLevelService;
        private readonly ICoolingNotificationService _coolingNotifications;
        private readonly IRoastPreferencesService _roastPreferences;
        private readonly JsonSerializerOptions _jsonOptions;
        private bool _isDisposed;

        // Property to get the current data file path
        public string DataFilePath
        {
            get => _appDataService.DataFilePath;
        }

        public RoastDataService(
            IAppDataService appDataService,
            IRoastLevelService roastLevelService,
            ICoolingNotificationService coolingNotifications,
            IRoastPreferencesService roastPreferences)
        {
            _appDataService = appDataService;
            _roastLevelService = roastLevelService;
            _coolingNotifications = coolingNotifications ?? throw new ArgumentNullException(nameof(coolingNotifications));
            _roastPreferences = roastPreferences ?? throw new ArgumentNullException(nameof(roastPreferences));

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
                    // Reload data with new path
                    await _appDataService.ReloadDataAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error reloading data after path change in RoastDataService: {ex.Message}");
                }
            });
        }

        private async Task<List<RoastData>> LoadRoastLogsAsync()
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

        public async Task ExportRoastLogAsync(
            Stream destination,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(destination);
            if (!destination.CanWrite)
            {
                throw new ArgumentException("The destination stream must be writable.", nameof(destination));
            }

            List<RoastData> allData = await LoadRoastLogsAsync();
            await using var writer = new StreamWriter(
                destination,
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                1024,
                leaveOpen: true);
            await writer.WriteLineAsync(
                "Date,Bean Type,Temperature,Batch Weight,Final Weight,Weight Loss %,Roast Time,Roast Level,Notes,Bean ID");

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
                    EscapeCsv(roast.Notes),
                    EscapeCsv(roast.BeanId?.ToString("D")));
                await writer.WriteLineAsync(line);
            }

            await writer.FlushAsync(cancellationToken);
        }

        private static string EscapeCsv(string? value)
        {
            string safeValue = value ?? string.Empty;
            return $"\"{safeValue.Replace("\"", "\"\"")}\"";
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

                RoastData? savedRoast = null;
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
                    savedRoast = existingRoast;
                    return true;
                });
                if (!saved)
                {
                    return false;
                }

                bool notificationsEnabled;
                try
                {
                    notificationsEnabled =
                        await _roastPreferences.GetCoolingNotificationsEnabledAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Cooling reminder preference read failed: {ex.Message}");
                    notificationsEnabled = false;
                }

                if (notificationsEnabled &&
                    savedRoast?.CompletionStatus == RoastCompletionStatus.AwaitingWeight &&
                    savedRoast is not { CoolingCompletedEarly: true } &&
                    savedRoast.ReadyToWeighAtUtc is DateTimeOffset readyAt)
                {
                    try
                    {
                        await _coolingNotifications.ScheduleCoolingReadyAsync(
                            savedRoast.Id,
                            readyAt,
                            savedRoast.BeanDisplaySnapshot,
                            savedRoast.BatchNumber);
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

        public async Task<RoastData?> GetLastRoastForBeanAsync(Guid beanId)
        {
            try
            {
                if (beanId == Guid.Empty)
                {
                    return null;
                }

                AppData appData = await _appDataService.LoadAppDataAsync();
                BeanData? bean = (appData.Beans ?? []).FirstOrDefault(candidate => candidate.Id == beanId);
                if (bean is null)
                {
                    return null;
                }

                return (appData.RoastLogs ?? [])
                    .Where(roast => RoastProjection.BelongsToBean(roast, bean, appData.Beans ?? []))
                    .OrderByDescending(RoastProjection.DroppedAtUtc)
                    .ThenByDescending(roast => roast.BatchNumber ?? 0)
                    .FirstOrDefault();
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

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _appDataService.DataFilePathChanged -= OnDataFilePathChanged;
        }
    }
}
