using System.Globalization;

namespace CafeMaestro.Services;

/// <summary>
/// Value parsing shared by the import adapters. Storage is invariant, so invariant wins; the
/// current culture is only a fallback for files exported by locale-aware spreadsheets.
/// </summary>
internal static class ImportValueParser
{
    private static readonly string[] DateFormats =
    [
        "dd/MM/yyyy",
        "MM/dd/yyyy",
        "yyyy-MM-dd",
        "dd-MM-yyyy",
        "yyyy/MM/dd"
    ];

    public static bool TryParseDate(string value, out DateTime result)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();

        return DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out result) ||
               DateTime.TryParseExact(trimmed, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result) ||
               DateTime.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.None, out result);
    }

    public static bool TryParseNumber(string value, out double result)
    {
        result = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = StripUnits(value);

        if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out result) &&
            !double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out result))
        {
            return false;
        }

        // "NaN" and "Infinity" parse successfully but are never a real weight, quantity,
        // temperature or percentage — and they cannot be serialized, so letting one through would
        // fail the whole atomic commit, valid rows included.
        if (!double.IsFinite(result))
        {
            result = 0;
            return false;
        }

        return true;
    }

    public static bool TryParsePrice(string value, out decimal result)
    {
        result = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();

        return decimal.TryParse(trimmed, NumberStyles.Currency, CultureInfo.InvariantCulture, out result) ||
               decimal.TryParse(trimmed, NumberStyles.Currency, CultureInfo.CurrentCulture, out result);
    }

    /// <summary>
    /// Parses roast durations. <c>mm:ss</c> is the recorded format, so a colon is never read as
    /// hours; a bare number is total seconds.
    /// </summary>
    public static bool TryParseDuration(string value, out int minutes, out int seconds)
    {
        minutes = 0;
        seconds = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();

        if (trimmed.Contains(':', StringComparison.Ordinal))
        {
            string[] parts = trimmed.Split(':');

            if (parts.Length == 2 &&
                int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedMinutes) &&
                int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedSeconds))
            {
                minutes = parsedMinutes;
                seconds = parsedSeconds;
                return true;
            }

            if (TimeSpan.TryParse(trimmed, CultureInfo.InvariantCulture, out TimeSpan span))
            {
                minutes = (span.Hours * 60) + span.Minutes;
                seconds = span.Seconds;
                return true;
            }

            return false;
        }

        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int totalSeconds) &&
            totalSeconds >= 0)
        {
            minutes = totalSeconds / 60;
            seconds = totalSeconds % 60;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Removes the unit suffixes people leave in exported columns (<c>240 g</c>, <c>218 °C</c>,
    /// <c>13.8 %</c>, <c>1.5 kg</c>) without touching the number itself.
    /// </summary>
    private static string StripUnits(string value)
    {
        string trimmed = value.Trim();

        foreach (string unit in (string[])["kg", "g", "°c", "c", "%"])
        {
            if (trimmed.Length > unit.Length &&
                trimmed.EndsWith(unit, StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[..^unit.Length].TrimEnd();
                break;
            }
        }

        return trimmed;
    }
}
