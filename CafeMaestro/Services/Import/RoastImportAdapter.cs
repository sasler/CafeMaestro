using CafeMaestro.Models;
using CafeMaestro.Navigation;

namespace CafeMaestro.Services;

/// <summary>
/// Roast log import: field definitions, row validation, duplicate policy, and the append that
/// runs inside the atomic commit.
/// </summary>
public sealed class RoastImportAdapter : IImportAdapter
{
    /// <summary>Matches the roast console default so an unspecified temperature stays plausible.</summary>
    internal const double DefaultTemperature = 235;

    private readonly IAppDataService _appDataService;
    private readonly IRoastLevelService _roastLevelService;

    public RoastImportAdapter(IAppDataService appDataService, IRoastLevelService roastLevelService)
    {
        _appDataService = appDataService ?? throw new ArgumentNullException(nameof(appDataService));
        _roastLevelService = roastLevelService ?? throw new ArgumentNullException(nameof(roastLevelService));
    }

    public ImportKindDescriptor Descriptor { get; } = new(
        ImportKind.Roasts,
        "Roast logs",
        "Dates, beans, weights, time, temperature, notes",
        "roast log",
        "roast logs",
        "Select CSV file with roast log data",
        "VIEW ROAST LOG",
        Routes.RoastLog);

    public IReadOnlyList<ImportFieldDefinition> Fields { get; } =
    [
        new("RoastDate", "Date", true, ["date", "roastdate"]),
        new("BeanType", "Coffee bean", true, ["coffee", "bean"], ExactAliases: ["type"]),
        new("Temperature", "Temperature", false, ["temp", "temperature"]),
        new("RoastTime", "Time", false, ["time", "duration"]),
        new("BatchWeight", "Batch weight", true, ["batch", "weight", "charge"], ExactAliases: ["weightg"]),
        new("FinalWeight", "Final weight", false, ["final", "drop"]),
        new("WeightLoss", "Loss percentage", false, ["loss", "shrink"]),
        new("Notes", "Notes", false, ["note", "notes", "comment"])
    ];

    public async Task<IImportSession> CreateSessionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AppData snapshot = await _appDataService.LoadAppDataAsync();
        List<RoastLevelData> roastLevels = await _roastLevelService.GetRoastLevelsAsync();
        return new RoastImportSession(snapshot.RoastLogs ?? [], roastLevels);
    }

    private sealed class RoastImportSession : IImportSession
    {
        private readonly List<RoastData> _accepted = [];
        private readonly List<RoastLevelData> _roastLevels;
        private readonly HashSet<string> _signatures;

        public RoastImportSession(IEnumerable<RoastData> existingRoasts, List<RoastLevelData> roastLevels)
        {
            _roastLevels = roastLevels;
            _signatures = new HashSet<string>(
                existingRoasts.Select(CreateSignature),
                StringComparer.OrdinalIgnoreCase);
        }

        public int AcceptedCount => _accepted.Count;

        public ImportRowOutcome Evaluate(
            int rowNumber,
            IReadOnlyDictionary<string, string> row,
            IReadOnlyDictionary<string, string> mappings)
        {
            ArgumentNullException.ThrowIfNull(row);
            ArgumentNullException.ThrowIfNull(mappings);

            string beanType = ImportHeaderMatcher.GetMappedValue(row, mappings, "BeanType")
                .Replace("  ", " ", StringComparison.Ordinal);
            string rawDate = ImportHeaderMatcher.GetMappedValue(row, mappings, "RoastDate");

            if (string.IsNullOrWhiteSpace(beanType))
            {
                return Reject(rowNumber, beanType, "Coffee bean is required.");
            }

            if (string.IsNullOrWhiteSpace(rawDate))
            {
                return Reject(rowNumber, beanType, "Date is required.");
            }

            if (!ImportValueParser.TryParseDate(rawDate, out DateTime roastDate))
            {
                return Reject(rowNumber, beanType, $"Date '{rawDate}' is not a recognised date.");
            }

            string rawBatchWeight = ImportHeaderMatcher.GetMappedValue(row, mappings, "BatchWeight");

            if (string.IsNullOrWhiteSpace(rawBatchWeight))
            {
                return Reject(rowNumber, beanType, "Batch weight is required.");
            }

            if (!ImportValueParser.TryParseNumber(rawBatchWeight, out double batchWeight) || batchWeight <= 0)
            {
                return Reject(rowNumber, beanType, $"Batch weight '{rawBatchWeight}' is not a weight above zero.");
            }

            var roast = new RoastData
            {
                Id = Guid.NewGuid(),
                BeanType = beanType,
                RoastDate = roastDate,
                BatchWeight = batchWeight,
                Temperature = DefaultTemperature,
                Notes = ImportHeaderMatcher.GetMappedValue(row, mappings, "Notes")
            };

            string rawTemperature = ImportHeaderMatcher.GetMappedValue(row, mappings, "Temperature");

            if (!string.IsNullOrWhiteSpace(rawTemperature))
            {
                if (!ImportValueParser.TryParseNumber(rawTemperature, out double temperature))
                {
                    return Reject(rowNumber, beanType, $"Temperature '{rawTemperature}' is not a number.");
                }

                roast.Temperature = temperature;
            }

            string rawTime = ImportHeaderMatcher.GetMappedValue(row, mappings, "RoastTime");

            if (!string.IsNullOrWhiteSpace(rawTime))
            {
                if (!ImportValueParser.TryParseDuration(rawTime, out int minutes, out int seconds))
                {
                    return Reject(rowNumber, beanType, $"Time '{rawTime}' is not a mm:ss duration.");
                }

                roast.RoastMinutes = minutes;
                roast.RoastSeconds = seconds;
            }

            string rawFinalWeight = ImportHeaderMatcher.GetMappedValue(row, mappings, "FinalWeight");

            if (!string.IsNullOrWhiteSpace(rawFinalWeight))
            {
                if (!ImportValueParser.TryParseNumber(rawFinalWeight, out double finalWeight))
                {
                    return Reject(rowNumber, beanType, $"Final weight '{rawFinalWeight}' is not a number.");
                }

                roast.FinalWeight = finalWeight;
            }

            string rawWeightLoss = ImportHeaderMatcher.GetMappedValue(row, mappings, "WeightLoss");

            // Loss percentage is a derived column: it only reconstructs a missing final weight.
            if (!roast.HasFinalWeight && !string.IsNullOrWhiteSpace(rawWeightLoss))
            {
                if (!ImportValueParser.TryParseNumber(rawWeightLoss, out double lossPercentage))
                {
                    return Reject(rowNumber, beanType, $"Loss percentage '{rawWeightLoss}' is not a number.");
                }

                if (lossPercentage is < 0 or > 100)
                {
                    return Reject(rowNumber, beanType, $"Loss percentage '{rawWeightLoss}' must be between 0 and 100.");
                }

                roast.FinalWeight = Math.Round(batchWeight * (1 - (lossPercentage / 100.0)), 2);
            }

            NewRoastDefaults.Apply(roast);

            roast.RoastLevelName = roast.HasFinalWeight
                ? RoastLevelResolver.Resolve(_roastLevels, roast.WeightLossPercentage)
                : "Pending";

            List<string> validationErrors = roast.Validate();

            if (validationErrors.Count > 0)
            {
                return Reject(rowNumber, beanType, validationErrors[0]);
            }

            string signature = CreateSignature(roast);

            if (!_signatures.Add(signature))
            {
                return Reject(
                    rowNumber,
                    beanType,
                    $"A {beanType} roast on {roast.RoastDate:d MMM yyyy} with the same weight and time is already logged.");
            }

            _accepted.Add(roast);

            return new ImportRowOutcome(
                rowNumber,
                true,
                $"{beanType} · {roast.RoastDate:d MMM yyyy}",
                roast.ResultMetrics);
        }

        public void Commit(AppData appData)
        {
            ArgumentNullException.ThrowIfNull(appData);
            appData.RoastLogs ??= [];
            appData.RoastLogs.AddRange(_accepted);
        }

        private static ImportRowOutcome Reject(int rowNumber, string beanType, string error)
        {
            string label = string.IsNullOrWhiteSpace(beanType) ? "no identifying values" : beanType;
            return new ImportRowOutcome(rowNumber, false, $"Row {rowNumber} · {label}", error);
        }

        /// <summary>
        /// Duplicate policy: the same bean, day, batch weight, temperature, and elapsed time —
        /// the signature the roast log already uses to strip duplicates after a restore.
        /// </summary>
        private static string CreateSignature(RoastData roast) =>
            $"{roast.BeanType?.Trim()}|{roast.RoastDate:yyyy-MM-dd}|{roast.BatchWeight}|{roast.Temperature}|{roast.RoastMinutes}:{roast.RoastSeconds}";
    }
}
