using CafeMaestro.Models;

namespace CafeMaestro.Services;

/// <summary>
/// Fills the roast workflow fields a record needs before it is appended to the log.
/// </summary>
/// <remarks>
/// A roast that arrives without a final weight is Awaiting weight and ready to weigh now — a
/// zero cooling duration makes <see cref="RoastData.ReadyToWeighAtUtc"/> equal to the drop time,
/// so imported history lands in the work queue as actionable rather than pretending to cool.
/// Only supported metadata may mark a record Unweighed instead.
/// </remarks>
public static class NewRoastDefaults
{
    public static void Apply(RoastData roast)
    {
        ArgumentNullException.ThrowIfNull(roast);

        roast.BeanDisplaySnapshot = string.IsNullOrWhiteSpace(roast.BeanDisplaySnapshot)
            ? roast.BeanType
            : roast.BeanDisplaySnapshot;

        if (!roast.DroppedAtUtc.HasValue && roast.RoastDate != default)
        {
            roast.DroppedAtUtc = V1ToV2AppDataMigration.ConvertLegacyRoastDate(roast.RoastDate);
        }

        if (roast.FinalWeight > 0)
        {
            roast.CompletionStatus = RoastCompletionStatus.Complete;
            return;
        }

        if (roast.FinalWeight is null or 0)
        {
            roast.FinalWeight = null;

            if (roast.CompletionStatus != RoastCompletionStatus.Unweighed &&
                roast.CompletionStatus != RoastCompletionStatus.Discarded)
            {
                roast.CompletionStatus = RoastCompletionStatus.AwaitingWeight;
            }

            roast.CoolingDurationSeconds ??= 0;
        }
    }
}
