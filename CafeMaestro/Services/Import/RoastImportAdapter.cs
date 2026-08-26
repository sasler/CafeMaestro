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

    /// <summary>
    /// What <c>IRoastDataService.ExportRoastLogAsync</c> writes in the loss column for a roast with
    /// no final weight. Kept in sync with the exporter so the app's own CSV round-trips.
    /// </summary>
    internal const string MissingWeightSentinel = "Pending";

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
        new("BeanId", "Bean ID", false, ["beanid", "identity"], ExactAliases: ["bean id"]),
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
        private readonly List<(RoastData Roast, ImportRowOutcome Outcome)> _accepted = [];
        private readonly List<RoastLevelData> _roastLevels;
        private readonly HashSet<string> _signatures;

        public RoastImportSession(
            IEnumerable<RoastData> existingRoasts,
            List<RoastLevelData> roastLevels)
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
            string rawBeanId = ImportHeaderMatcher.GetMappedValue(row, mappings, "BeanId");
            string rawDate = ImportHeaderMatcher.GetMappedValue(row, mappings, "RoastDate");

            if (string.IsNullOrWhiteSpace(beanType))
            {
                return Reject(rowNumber, beanType, "Coffee bean is required.");
            }

            if (string.IsNullOrWhiteSpace(rawDate))
            {
                return Reject(rowNumber, beanType, "Date is required.");
            }

            Guid? beanId = null;
            if (!string.IsNullOrWhiteSpace(rawBeanId))
            {
                if (!Guid.TryParse(rawBeanId, out Guid parsedBeanId) || parsedBeanId == Guid.Empty)
                {
                    return Reject(rowNumber, beanType, $"Bean ID '{rawBeanId}' is not a valid stable identity.");
                }

                // A supplied identity is authoritative. It may refer to a bean that is not in
                // this dataset yet; retaining it is safer than reassigning history by name.
                beanId = parsedBeanId;
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
                BeanId = beanId,
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

            // "Supplied" is tracked separately from the model's HasFinalWeight predicate, which is
            // FinalWeight > 0. Conflating them would let a supplied 0 or a supplied negative weight
            // look like an absent one and be silently replaced by a value derived from loss.
            bool finalWeightSupplied = false;

            if (!string.IsNullOrWhiteSpace(rawFinalWeight) && !IsAbsentFinalWeight(rawFinalWeight))
            {
                if (!ImportValueParser.TryParseNumber(rawFinalWeight, out double finalWeight))
                {
                    return Reject(rowNumber, beanType, $"Final weight '{rawFinalWeight}' is not a number.");
                }

                if (finalWeight < 0)
                {
                    return Reject(rowNumber, beanType, $"Final weight '{rawFinalWeight}' must be zero or greater.");
                }

                finalWeightSupplied = true;
                roast.FinalWeight = finalWeight;
            }

            string rawWeightLoss = ImportHeaderMatcher.GetMappedValue(row, mappings, "WeightLoss");

            // Loss percentage is a derived column: it only reconstructs a final weight the file did
            // not supply, and never overrides one it did.
            if (!finalWeightSupplied &&
                !string.IsNullOrWhiteSpace(rawWeightLoss) &&
                !IsPendingSentinel(rawWeightLoss))
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

            var outcome = new ImportRowOutcome(
                rowNumber,
                true,
                $"{beanType} · {roast.RoastDate:d MMM yyyy}",
                roast.ResultMetrics);
            _accepted.Add((roast, outcome));

            return outcome;
        }

        public IReadOnlyList<ImportRowOutcome> Commit(AppData appData)
        {
            ArgumentNullException.ThrowIfNull(appData);
            appData.RoastLogs ??= [];

            var current = new HashSet<string>(
                appData.RoastLogs.Select(CreateSignature),
                StringComparer.OrdinalIgnoreCase);
            var droppedAtCommit = new List<ImportRowOutcome>();

            foreach ((RoastData roast, ImportRowOutcome outcome) in _accepted)
            {
                if (!current.Add(CreateSignature(roast)))
                {
                    droppedAtCommit.Add(outcome with
                    {
                        IsAccepted = false,
                        Detail = $"A matching {roast.BeanType} roast was logged while this import was being reviewed."
                    });
                    continue;
                }

                appData.RoastLogs.Add(roast);
            }

            return droppedAtCommit;
        }

        /// <summary>
        /// CafeMaestro's roast-log export writes <c>0</c> in the final-weight column and
        /// <c>Pending</c> in the loss column for a roast that has no final weight yet. Both mean
        /// "not recorded", so the row must round-trip back onto the Awaiting weight path rather
        /// than being read as a real zero or rejected as an unparsable percentage.
        /// </summary>
        private static bool IsAbsentFinalWeight(string value) =>
            IsPendingSentinel(value) ||
            (ImportValueParser.TryParseNumber(value, out double weight) && weight == 0);

        private static bool IsPendingSentinel(string value) =>
            string.Equals(value.Trim(), MissingWeightSentinel, StringComparison.OrdinalIgnoreCase);

        private static ImportRowOutcome Reject(int rowNumber, string beanType, string error)
        {
            string label = string.IsNullOrWhiteSpace(beanType) ? "no identifying values" : beanType;
            return new ImportRowOutcome(rowNumber, false, $"Row {rowNumber} · {label}", error);
        }

        /// <summary>
        /// Duplicate policy: the same bean, day, batch weight, temperature, and elapsed time —
        /// the signature the roast log already uses to strip duplicates after a restore.
        /// </summary>
        private static string CreateSignature(RoastData roast)
        {
            string beanIdentity = roast.BeanId?.ToString("D") ?? roast.BeanType?.Trim() ?? string.Empty;
            return $"{beanIdentity}|{roast.RoastDate:yyyy-MM-dd}|{roast.BatchWeight}|{roast.Temperature}|{roast.RoastMinutes}:{roast.RoastSeconds}";
        }
    }
}
