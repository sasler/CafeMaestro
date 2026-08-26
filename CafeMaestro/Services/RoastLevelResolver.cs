using CafeMaestro.Models;

namespace CafeMaestro.Services;

/// <summary>
/// Maps a weight-loss percentage onto a configured roast level. Shared so a roast imported in
/// bulk is named exactly like a roast saved one at a time.
/// </summary>
public static class RoastLevelResolver
{
    public const string UnknownLevelName = "Unknown";

    public static string Resolve(IReadOnlyCollection<RoastLevelData> levels, double weightLossPercentage)
    {
        ArgumentNullException.ThrowIfNull(levels);

        foreach (RoastLevelData level in levels.OrderBy(level => level.MinWeightLossPercentage))
        {
            if (weightLossPercentage >= level.MinWeightLossPercentage &&
                weightLossPercentage < level.MaxWeightLossPercentage)
            {
                return level.Name;
            }
        }

        RoastLevelData? highestLevel = levels
            .OrderByDescending(level => level.MaxWeightLossPercentage)
            .FirstOrDefault();

        return highestLevel?.Name ?? UnknownLevelName;
    }
}
