using CafeMaestro.Models;

namespace CafeMaestro.Services;

internal static class AppDataNormalizer
{
    public static void Normalize(AppData data, bool allowLegacyRepairs)
    {
        if (!allowLegacyRepairs)
        {
            return;
        }

        data.Beans ??= [];
        data.RoastLogs ??= [];
        data.RoastLevels ??= [];
        if (data.RoastLevels.Count == 0)
        {
            data.RoastLevels = AppDataFactory.CreateDefault().RoastLevels;
        }

        data.AppVersion = string.IsNullOrWhiteSpace(data.AppVersion) ? "Unknown" : data.AppVersion;
        foreach (RoastData roast in data.RoastLogs)
        {
            if (string.IsNullOrWhiteSpace(roast.BeanDisplaySnapshot))
            {
                roast.BeanDisplaySnapshot = roast.BeanType;
            }

            if (string.IsNullOrWhiteSpace(roast.BeanType))
            {
                roast.BeanType = roast.BeanDisplaySnapshot;
            }
        }
    }

    public static List<string> GetValidationErrors(AppData data)
    {
        var errors = new List<string>();
        if (data.DataSchemaVersion != AppDataSchema.CurrentVersion)
        {
            errors.Add(
                $"DataSchemaVersion must be {AppDataSchema.CurrentVersion}, but was {data.DataSchemaVersion}.");
        }

        if (data.Beans is null)
        {
            errors.Add("Beans collection must not be null.");
        }
        else
        {
            errors.AddRange(data.Beans.SelectMany((bean, index) =>
                bean.Validate().Select(error => $"Bean {index + 1}: {error}")));
        }

        if (data.RoastLogs is null)
        {
            errors.Add("RoastLogs collection must not be null.");
        }
        else
        {
            errors.AddRange(data.RoastLogs.SelectMany((roast, index) =>
                roast.Validate().Select(error => $"Roast {index + 1}: {error}")));
            errors.AddRange(data.RoastLogs.SelectMany((roast, index) =>
                string.IsNullOrWhiteSpace(roast.BeanDisplaySnapshot)
                    ? new[] { $"Roast {index + 1}: BeanDisplaySnapshot must not be empty." }
                    : []));
            errors.AddRange(data.RoastLogs.SelectMany((roast, index) =>
                GetWorkflowErrors(roast).Select(error => $"Roast {index + 1}: {error}")));
        }

        if (data.RoastLevels is null)
        {
            errors.Add("RoastLevels collection must not be null.");
        }
        else
        {
            errors.AddRange(data.RoastLevels.SelectMany((level, index) =>
                level.Validate().Select(error => $"Roast level {index + 1}: {error}")));
        }
        if (data.ActiveRoastSession is not null)
        {
            errors.AddRange(data.ActiveRoastSession.Validate());
            ActiveRoastDraft? activeRoast = data.ActiveRoastSession.ActiveRoast;
            if (activeRoast is not null &&
                activeRoast.BeanId != Guid.Empty &&
                data.Beans is not null &&
                !data.Beans.Any(bean => bean.Id == activeRoast.BeanId))
            {
                errors.Add("Active roast BeanId must reference an existing bean.");
            }
        }

        return errors;
    }

    private static IEnumerable<string> GetWorkflowErrors(RoastData roast)
    {
        if (roast.CompletionStatus == RoastCompletionStatus.AwaitingWeight &&
            (!roast.DroppedAtUtc.HasValue || !roast.CoolingDurationSeconds.HasValue))
        {
            yield return "Awaiting-weight roasts require DroppedAtUtc and CoolingDurationSeconds.";
        }

        if (roast.CompletionStatus == RoastCompletionStatus.AwaitingWeight &&
            roast.FinalWeight is not null)
        {
            yield return "Awaiting-weight roasts cannot contain FinalWeight.";
        }

        if (roast.CompletionStatus is RoastCompletionStatus.Unweighed or RoastCompletionStatus.Discarded &&
            roast.FinalWeight is not null)
        {
            yield return "Unweighed or discarded roasts cannot contain FinalWeight.";
        }
    }
}
