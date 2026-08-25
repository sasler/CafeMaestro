namespace CafeMaestro.Models;

public sealed class RoastSessionData
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset StartedAtUtc { get; set; }
    public int NextBatchNumber { get; set; } = 1;
    public ActiveRoastDraft? ActiveRoast { get; set; }

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (Id == Guid.Empty)
        {
            errors.Add("Active roast session Id must not be empty.");
        }

        if (StartedAtUtc == default)
        {
            errors.Add("Active roast session StartedAtUtc must be set.");
        }

        if (NextBatchNumber <= 0)
        {
            errors.Add("Active roast session NextBatchNumber must be greater than 0.");
        }

        if (ActiveRoast is not null)
        {
            errors.AddRange(ActiveRoast.Validate(this));
        }

        return errors;
    }
}

public sealed class ActiveRoastDraft
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public int BatchNumber { get; set; }
    public Guid BeanId { get; set; }
    public string BeanDisplaySnapshot { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public double BatchWeight { get; set; }
    public ActiveRoastPhase Phase { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? RunningSinceUtc { get; set; }
    public double AccumulatedElapsedSeconds { get; set; }
    public int? FirstCrackElapsedSeconds { get; set; }
    public bool FirstCrackEnabled { get; set; }
    public int CoolingDurationSeconds { get; set; } = 300;

    internal List<string> Validate(RoastSessionData session)
    {
        var errors = new List<string>();
        if (Id == Guid.Empty)
        {
            errors.Add("Active roast Id must not be empty.");
        }

        if (SessionId == Guid.Empty || SessionId != session.Id)
        {
            errors.Add("Active roast SessionId must match its session.");
        }

        if (BatchNumber <= 0 || BatchNumber != session.NextBatchNumber)
        {
            errors.Add("Active roast BatchNumber must match the next session batch number.");
        }

        if (BeanId == Guid.Empty)
        {
            errors.Add("Active roast BeanId must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(BeanDisplaySnapshot))
        {
            errors.Add("Active roast BeanDisplaySnapshot must not be empty.");
        }

        if (!double.IsFinite(Temperature) || Temperature <= 0 || Temperature > 500)
        {
            errors.Add("Active roast Temperature must be greater than 0 and less than or equal to 500.");
        }

        if (!double.IsFinite(BatchWeight) || BatchWeight <= 0)
        {
            errors.Add("Active roast BatchWeight must be greater than 0.");
        }

        if (!Enum.IsDefined(Phase))
        {
            errors.Add("Active roast Phase is not supported.");
        }

        if (StartedAtUtc == default || StartedAtUtc < session.StartedAtUtc)
        {
            errors.Add("Active roast StartedAtUtc must be within its session.");
        }

        if (Phase == ActiveRoastPhase.Roasting && !RunningSinceUtc.HasValue)
        {
            errors.Add("Active roast RunningSinceUtc is required while roasting.");
        }

        if (Phase == ActiveRoastPhase.Paused && RunningSinceUtc.HasValue)
        {
            errors.Add("Active roast RunningSinceUtc must be empty while paused.");
        }

        if (RunningSinceUtc.HasValue && RunningSinceUtc < StartedAtUtc)
        {
            errors.Add("Active roast RunningSinceUtc must not precede StartedAtUtc.");
        }

        if (!double.IsFinite(AccumulatedElapsedSeconds) || AccumulatedElapsedSeconds < 0)
        {
            errors.Add("Active roast AccumulatedElapsedSeconds must be finite and nonnegative.");
        }

        if (FirstCrackElapsedSeconds < 0)
        {
            errors.Add("Active roast FirstCrackElapsedSeconds must be nonnegative when present.");
        }

        if (!FirstCrackEnabled && FirstCrackElapsedSeconds.HasValue)
        {
            errors.Add("Active roast FirstCrackElapsedSeconds requires FirstCrackEnabled.");
        }

        if (Phase == ActiveRoastPhase.Paused &&
            FirstCrackElapsedSeconds > AccumulatedElapsedSeconds)
        {
            errors.Add(
                "Active roast FirstCrackElapsedSeconds cannot exceed accumulated elapsed time while paused.");
        }

        if (CoolingDurationSeconds < 0)
        {
            errors.Add("Active roast CoolingDurationSeconds must be nonnegative.");
        }
        else
        {
            try
            {
                _ = StartedAtUtc.AddSeconds(CoolingDurationSeconds);
            }
            catch (ArgumentOutOfRangeException)
            {
                errors.Add("Active roast cooling projection exceeds the supported date range.");
            }
        }

        return errors;
    }
}

public enum ActiveRoastPhase
{
    Roasting,
    Paused
}
