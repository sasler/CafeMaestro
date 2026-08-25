using System.Text.Json;
using CafeMaestro.Models;

namespace CafeMaestro.Services;

internal static class AppDataJsonReader
{
    private static readonly string[] CollectionProperties =
    [
        nameof(AppData.Beans),
        nameof(AppData.RoastLogs),
        nameof(AppData.RoastLevels)
    ];

    private static readonly string[] KnownProperties =
    [
        nameof(AppData.DataSchemaVersion),
        nameof(AppData.Beans),
        nameof(AppData.RoastLogs),
        nameof(AppData.RoastLevels),
        nameof(AppData.ActiveRoastSession),
        nameof(AppData.LastModified),
        nameof(AppData.AppVersion)
    ];

    public static async Task<AppData> DeserializeAsync(
        Stream stream,
        JsonSerializerOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);

        using JsonDocument document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);
        ValidateStructure(document.RootElement);

        if (TryGetProperty(
                document.RootElement,
                nameof(AppData.DataSchemaVersion),
                out JsonElement schemaVersion) &&
            schemaVersion.ValueKind == JsonValueKind.Number &&
            schemaVersion.TryGetInt32(out int version) &&
            version == AppDataSchema.CurrentVersion)
        {
            ValidateCurrentSchemaPresence(document.RootElement);
        }

        AppData? data = document.RootElement.Deserialize<AppData>(options);
        if (data is null)
        {
            throw new InvalidDataException(
                "The selected file does not contain CafeMaestro data.");
        }

        bool hasSchemaVersion = document.RootElement
            .EnumerateObject()
            .Any(property => string.Equals(
                property.Name,
                nameof(AppData.DataSchemaVersion),
                StringComparison.OrdinalIgnoreCase));
        if (!hasSchemaVersion)
        {
            data.DataSchemaVersion = AppDataSchema.LegacyVersion;
        }

        return data;
    }

    public static async Task<bool> IsLegacySchemaAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using JsonDocument document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!TryGetProperty(root, nameof(AppData.DataSchemaVersion), out JsonElement version))
        {
            return true;
        }

        return version.ValueKind == JsonValueKind.Number &&
            version.TryGetInt32(out int parsedVersion) &&
            parsedVersion == AppDataSchema.LegacyVersion;
    }

    private static void ValidateStructure(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                "The selected file does not have the required CafeMaestro data structure.");
        }

        JsonProperty[] properties = root.EnumerateObject().ToArray();
        bool hasSchemaVersion = properties.Any(property => string.Equals(
            property.Name,
            nameof(AppData.DataSchemaVersion),
            StringComparison.OrdinalIgnoreCase));
        int? declaredSchemaVersion = properties
            .Where(property => string.Equals(
                property.Name,
                nameof(AppData.DataSchemaVersion),
                StringComparison.OrdinalIgnoreCase))
            .Select(property => property.Value.TryGetInt32(out int version)
                ? version
                : (int?)null)
            .FirstOrDefault();
        bool hasDataCollection = properties.Any(property =>
            CollectionProperties.Contains(property.Name, StringComparer.OrdinalIgnoreCase));
        bool hasKnownProperty = properties.Any(property =>
            KnownProperties.Contains(property.Name, StringComparer.OrdinalIgnoreCase));
        bool requiresLegacyCollection =
            !hasSchemaVersion ||
            declaredSchemaVersion <= AppDataSchema.LegacyVersion;
        if (!hasKnownProperty || (requiresLegacyCollection && !hasDataCollection))
        {
            throw new InvalidDataException(
                "The selected file does not have the required CafeMaestro data structure.");
        }

        foreach (JsonProperty property in properties)
        {
            if (CollectionProperties.Contains(property.Name, StringComparer.OrdinalIgnoreCase) &&
                property.Value.ValueKind is not JsonValueKind.Array and not JsonValueKind.Null)
            {
                throw new InvalidDataException(
                    "The selected file does not have the required CafeMaestro data structure.");
            }

            if (CollectionProperties.Contains(property.Name, StringComparer.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.Array &&
                property.Value.EnumerateArray().Any(element => element.ValueKind != JsonValueKind.Object))
            {
                throw new InvalidDataException(
                    $"The {property.Name} collection contains an invalid element.");
            }
        }
    }

    private static void ValidateCurrentSchemaPresence(JsonElement root)
    {
        foreach (string collectionName in CollectionProperties)
        {
            if (!TryGetProperty(root, collectionName, out JsonElement collection) ||
                collection.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException(
                    $"The current-schema data requires {collectionName} as an array.");
            }
        }

        TryGetProperty(root, nameof(AppData.RoastLogs), out JsonElement roastLogs);
        foreach (JsonElement roast in roastLogs.EnumerateArray())
        {
            RequireProperties(
                roast,
                "roast",
                nameof(RoastData.Id),
                nameof(RoastData.BeanType),
                nameof(RoastData.BeanDisplaySnapshot),
                nameof(RoastData.CompletionStatus));
            TryGetProperty(
                roast,
                nameof(RoastData.BeanDisplaySnapshot),
                out JsonElement snapshot);
            if (snapshot.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(snapshot.GetString()))
            {
                throw new InvalidDataException(
                    "The current-schema roast requires a nonempty BeanDisplaySnapshot.");
            }
        }

        if (!TryGetProperty(root, nameof(AppData.ActiveRoastSession), out JsonElement session) ||
            session.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (session.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("ActiveRoastSession must be an object when present.");
        }

        RequireProperties(
            session,
            "active roast session",
            nameof(RoastSessionData.Id),
            nameof(RoastSessionData.StartedAtUtc),
            nameof(RoastSessionData.NextBatchNumber));

        if (!TryGetProperty(session, nameof(RoastSessionData.ActiveRoast), out JsonElement draft) ||
            draft.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (draft.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("ActiveRoast must be an object when present.");
        }

        RequireProperties(
            draft,
            "active roast",
            nameof(ActiveRoastDraft.Id),
            nameof(ActiveRoastDraft.SessionId),
            nameof(ActiveRoastDraft.BatchNumber),
            nameof(ActiveRoastDraft.BeanId),
            nameof(ActiveRoastDraft.BeanDisplaySnapshot),
            nameof(ActiveRoastDraft.Temperature),
            nameof(ActiveRoastDraft.BatchWeight),
            nameof(ActiveRoastDraft.Phase),
            nameof(ActiveRoastDraft.StartedAtUtc),
            nameof(ActiveRoastDraft.AccumulatedElapsedSeconds),
            nameof(ActiveRoastDraft.FirstCrackEnabled),
            nameof(ActiveRoastDraft.CoolingDurationSeconds));
    }

    private static void RequireProperties(
        JsonElement element,
        string description,
        params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (!TryGetProperty(element, propertyName, out _))
            {
                throw new InvalidDataException(
                    $"The current-schema {description} is missing required property {propertyName}.");
            }
        }
    }

    private static bool TryGetProperty(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
