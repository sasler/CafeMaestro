using System.Globalization;
using CafeMaestro.Services;

namespace CafeMaestro.ViewModels.Popups;

public sealed record WeightValidationResult(bool IsValid, double? Grams, string? Error);

public static class WeighInInputValidator
{
    public static WeightValidationResult Validate(string? input, double batchWeight)
    {
        if (!TryParse(input, out double grams) || !double.IsFinite(grams) || grams <= 0)
        {
            return new(false, null, "Enter a final weight greater than 0 g.");
        }

        double normalized = RoastPreferenceDefaults.NormalizeGrams(grams);
        if (Math.Abs(grams - normalized) > 0.000_001)
        {
            return new(false, null, "Use 0.1 g precision.");
        }

        if (normalized > batchWeight)
        {
            return new(false, null,
                $"More than the {batchWeight:0.#} g loaded — did you weigh both batches together?");
        }

        return new(true, normalized, null);
    }

    private static bool TryParse(string? input, out double grams) =>
        double.TryParse(input, NumberStyles.Number, CultureInfo.CurrentCulture, out grams) ||
        double.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out grams);
}
