using System.Globalization;
using CafeMaestro.Models;

namespace CafeMaestro.ViewModels;

/// <summary>A read-only card projection shared by open work, history, and roast detail.</summary>
public sealed class RoastLogCard
{
    private RoastLogCard() { }

    public required Guid RoastId { get; init; }
    public required string BeanDisplay { get; init; }
    public required string BatchDisplay { get; init; }
    public required string StatusDisplay { get; init; }
    public required string StatusDetail { get; init; }
    public required string DateDisplay { get; init; }
    public required string InputDisplay { get; init; }
    public required string OutputDisplay { get; init; }
    public required string LossDisplay { get; init; }
    public required string LevelDisplay { get; init; }
    public required string TemperatureDisplay { get; init; }
    public required string RoastTimeDisplay { get; init; }
    public required string FirstCrackDisplay { get; init; }
    public required string SemanticDescription { get; init; }
    public required RoastEffectiveStatus Status { get; init; }
    public required string SearchableText { get; init; }
    public RoastWorkItem? WorkItem { get; init; }
    public RoastData? Roast { get; init; }

    public bool IsCooling => Status == RoastEffectiveStatus.Cooling;
    public bool IsNeedsWeight => Status == RoastEffectiveStatus.NeedsWeight;
    public bool IsComplete => Status == RoastEffectiveStatus.Complete;
    public bool IsUnweighed => Status == RoastEffectiveStatus.Unweighed;
    public bool IsDiscarded => Status == RoastEffectiveStatus.Discarded;
    public bool CanWeigh => IsNeedsWeight;
    public bool HasFirstCrack => FirstCrackDisplay != "—";

    public bool Matches(string searchText) =>
        string.IsNullOrWhiteSpace(searchText) ||
        SearchableText.Contains(searchText.Trim(), StringComparison.OrdinalIgnoreCase);

    public static RoastLogCard FromWork(RoastWorkItem item, RoastData? roast = null)
    {
        string batch = item.BatchNumber is int number ? $"Batch {number}" : "Roast batch";
        string status = item.Status == RoastEffectiveStatus.Cooling ? "Cooling" : "Needs weight";
        string detail = item.Status == RoastEffectiveStatus.Cooling
            ? $"Ready in {FormatCountdown(item.RemainingCoolingSeconds)}"
            : "Ready to weigh";
        string date = item.DroppedAtUtc.ToLocalTime().ToString("d MMM · HH:mm", CultureInfo.CurrentCulture);
        string time = FormatTime(item.TotalSeconds);
        string semantic = $"{status}, {batch}, {item.BeanDisplaySnapshot}, dropped {date}, " +
            $"{item.BatchWeight:0.0} grams in, roast time {time}. {detail}.";

        return new RoastLogCard
        {
            RoastId = item.RoastId,
            BeanDisplay = item.BeanDisplaySnapshot,
            BatchDisplay = batch,
            StatusDisplay = status,
            StatusDetail = detail,
            DateDisplay = date,
            InputDisplay = $"{item.BatchWeight:0.0} g",
            OutputDisplay = "—",
            LossDisplay = "—",
            LevelDisplay = "—",
            TemperatureDisplay = $"{item.Temperature:0.#} °C",
            RoastTimeDisplay = time,
            FirstCrackDisplay = roast?.FirstCrackSeconds.HasValue == true ? roast.FirstCrackTime : "—",
            SemanticDescription = semantic,
            Status = item.Status,
            SearchableText = $"{item.BeanDisplaySnapshot} {batch} {status} {item.Notes} {item.Summary} {item.RoastLevelName} {roast?.Notes}",
            WorkItem = item,
            Roast = roast
        };
    }

    public static RoastLogCard FromHistory(RoastData roast)
    {
        RoastEffectiveStatus status = roast.CompletionStatus switch
        {
            RoastCompletionStatus.Complete => RoastEffectiveStatus.Complete,
            RoastCompletionStatus.Unweighed => RoastEffectiveStatus.Unweighed,
            RoastCompletionStatus.Discarded => RoastEffectiveStatus.Discarded,
            _ => RoastEffectiveStatus.NeedsWeight
        };
        string bean = string.IsNullOrWhiteSpace(roast.BeanDisplaySnapshot)
            ? roast.BeanType
            : roast.BeanDisplaySnapshot;
        string batch = roast.BatchNumber is int number ? $"Batch {number}" : "Roast";
        string statusText = status switch
        {
            RoastEffectiveStatus.Complete => "Complete",
            RoastEffectiveStatus.Unweighed => "Unweighed",
            RoastEffectiveStatus.Discarded => "Discarded",
            _ => "Needs weight"
        };
        bool complete = status == RoastEffectiveStatus.Complete && roast.FinalWeight is > 0;
        string output = complete ? $"{roast.FinalWeight:0.0} g" : "—";
        string loss = complete ? $"{roast.WeightLossPercentage:0.0}%" : "—";
        string level = complete && !string.IsNullOrWhiteSpace(roast.RoastLevelName)
            ? roast.RoastLevelName
            : "—";
        DateTimeOffset droppedAt = roast.DroppedAtUtc ?? new DateTimeOffset(roast.RoastDate);
        string date = droppedAt.ToLocalTime().ToString("d MMM yyyy · HH:mm", CultureInfo.CurrentCulture);
        string firstCrack = roast.FirstCrackSeconds.HasValue ? roast.FirstCrackTime : "—";
        string statusDetail = status switch
        {
            RoastEffectiveStatus.Complete => $"{loss} loss · {level}",
            RoastEffectiveStatus.Unweighed => "No final weight recorded",
            RoastEffectiveStatus.Discarded => "Batch discarded",
            _ => "Ready to weigh"
        };

        return new RoastLogCard
        {
            RoastId = roast.Id,
            BeanDisplay = bean,
            BatchDisplay = batch,
            StatusDisplay = statusText,
            StatusDetail = statusDetail,
            DateDisplay = date,
            InputDisplay = $"{roast.BatchWeight:0.0} g",
            OutputDisplay = output,
            LossDisplay = loss,
            LevelDisplay = level,
            TemperatureDisplay = $"{roast.Temperature:0.#} °C",
            RoastTimeDisplay = roast.FormattedTime,
            FirstCrackDisplay = firstCrack,
            SemanticDescription = $"{statusText}, {batch}, {bean}, {date}, {roast.BatchWeight:0.0} grams in, " +
                $"{output} out, {loss} loss, roast time {roast.FormattedTime}.",
            Status = status,
            SearchableText = $"{bean} {roast.Notes} {roast.Summary} {roast.RoastLevelName} {statusText}",
            Roast = roast
        };
    }

    private static string FormatCountdown(double seconds)
    {
        int whole = Math.Max(0, (int)Math.Ceiling(seconds));
        return $"{whole / 60:00}:{whole % 60:00}";
    }

    private static string FormatTime(int seconds) => $"{Math.Max(0, seconds) / 60:00}:{Math.Max(0, seconds) % 60:00}";
}
