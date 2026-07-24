using System.Text.Json;
using CafeMaestro.Models;

namespace CafeMaestro.Services;

internal static class AppDataJsonReader
{
    private static readonly string[] RequiredArrayProperties =
    [
        nameof(AppData.Beans),
        nameof(AppData.RoastLogs)
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

        AppData? data = document.RootElement.Deserialize<AppData>(options);
        return data ??
               throw new InvalidDataException(
                   "The selected file does not contain CafeMaestro data.");
    }

    private static void ValidateStructure(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                "The selected file does not have the required CafeMaestro data structure.");
        }

        foreach (string requiredProperty in RequiredArrayProperties)
        {
            bool hasRequiredArray = root
                .EnumerateObject()
                .Any(candidate =>
                    string.Equals(
                        candidate.Name,
                        requiredProperty,
                        StringComparison.OrdinalIgnoreCase) &&
                    candidate.Value.ValueKind == JsonValueKind.Array);

            if (!hasRequiredArray)
            {
                throw new InvalidDataException(
                    "The selected file does not have the required CafeMaestro data structure.");
            }
        }
    }
}
