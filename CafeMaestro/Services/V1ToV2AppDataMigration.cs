using CafeMaestro.Models;

namespace CafeMaestro.Services;

public sealed class V1ToV2AppDataMigration : IAppDataMigration
{
    public int SourceVersion => AppDataSchema.LegacyVersion;
    public int TargetVersion => AppDataSchema.CurrentVersion;

    public void Migrate(AppData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.DataSchemaVersion != SourceVersion)
        {
            throw new InvalidOperationException(
                $"Migration {SourceVersion} to {TargetVersion} cannot process schema {data.DataSchemaVersion}.");
        }

        data.Beans ??= [];
        data.RoastLogs ??= [];
        data.RoastLevels ??= [];
        data.ActiveRoastSession = null;

        foreach (RoastData roast in data.RoastLogs)
        {
            if (string.IsNullOrWhiteSpace(roast.BeanDisplaySnapshot))
            {
                roast.BeanDisplaySnapshot = roast.BeanType;
            }

            BeanData[] exactMatches = data.Beans
                .Where(bean => string.Equals(
                    bean.DisplayName,
                    roast.BeanDisplaySnapshot,
                    StringComparison.Ordinal))
                .Take(2)
                .ToArray();
            roast.BeanId = exactMatches.Length == 1 ? exactMatches[0].Id : null;

            roast.DroppedAtUtc = ConvertLegacyRoastDate(roast.RoastDate);
            if (roast.FinalWeight > 0)
            {
                roast.CompletionStatus = RoastCompletionStatus.Complete;
            }
            else if (roast.FinalWeight is null or 0)
            {
                roast.FinalWeight = null;
                roast.CompletionStatus = RoastCompletionStatus.AwaitingWeight;
                roast.CoolingDurationSeconds = 0;
            }
        }

        data.DataSchemaVersion = TargetVersion;
    }

    internal static DateTimeOffset ConvertLegacyRoastDate(DateTime roastDate)
    {
        DateTime utc = roastDate.Kind switch
        {
            DateTimeKind.Utc => roastDate,
            DateTimeKind.Local => roastDate.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(roastDate, DateTimeKind.Local).ToUniversalTime(),
            _ => throw new ArgumentOutOfRangeException(nameof(roastDate))
        };

        return new DateTimeOffset(utc);
    }
}
