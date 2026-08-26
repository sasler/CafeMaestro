namespace CafeMaestro.Services;

/// <summary>
/// Shared auto-mapping. Both import kinds score CSV headers the same way; only the field
/// definitions differ.
/// </summary>
public static class ImportHeaderMatcher
{
    public const string NoneOption = "-- None --";

    /// <summary>
    /// Picks the best header for each field. Fields with no plausible header are left unmapped.
    /// </summary>
    public static Dictionary<string, string> SuggestMappings(
        IEnumerable<ImportFieldDefinition> fields,
        IEnumerable<string> headers)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(headers);

        List<string> candidates = headers.Where(IsSelectableHeader).ToList();
        var mappings = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (ImportFieldDefinition field in fields)
        {
            string? header = FindBestHeader(field, candidates);

            if (header is not null)
            {
                mappings[field.PropertyKey] = header;
            }
        }

        return mappings;
    }

    public static bool IsSelectableHeader(string? header)
    {
        return !string.IsNullOrWhiteSpace(header) &&
               !string.Equals(header, NoneOption, StringComparison.Ordinal);
    }

    /// <summary>
    /// Reads a mapped cell. Returns an empty string when the field is unmapped or the column is
    /// absent from this row.
    /// </summary>
    public static string GetMappedValue(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, string> mappings,
        string propertyKey)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(mappings);

        if (mappings.TryGetValue(propertyKey, out string? header) &&
            IsSelectableHeader(header) &&
            row.TryGetValue(header, out string? value))
        {
            return value?.Trim() ?? string.Empty;
        }

        return string.Empty;
    }

    public static string Normalize(string value)
    {
        return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    private static string? FindBestHeader(
        ImportFieldDefinition field,
        IReadOnlyCollection<string> headers)
    {
        string displayName = Normalize(field.DisplayName.Replace("*", string.Empty, StringComparison.Ordinal));
        string propertyName = Normalize(field.PropertyKey);

        return headers
            .Select(header => new
            {
                Header = header,
                Score = ScoreHeader(header, displayName, propertyName, field)
            })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Header, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => candidate.Header)
            .FirstOrDefault();
    }

    private static int ScoreHeader(
        string header,
        string displayName,
        string propertyName,
        ImportFieldDefinition field)
    {
        string normalizedHeader = Normalize(header);
        int score = 0;

        if (normalizedHeader == displayName || normalizedHeader == propertyName)
        {
            score += 100;
        }

        foreach (string alias in field.ExactAliases ?? [])
        {
            if (normalizedHeader == Normalize(alias))
            {
                score += 90;
            }
        }

        foreach (string alias in field.ContainsAliases ?? [])
        {
            if (normalizedHeader.Contains(Normalize(alias), StringComparison.Ordinal))
            {
                score += 90;
            }
        }

        foreach (string keyword in field.Keywords)
        {
            string normalizedKeyword = Normalize(keyword);

            if (normalizedKeyword.Length == 0)
            {
                continue;
            }

            if (normalizedHeader == normalizedKeyword)
            {
                score += 50;
            }
            else if (normalizedHeader.Contains(normalizedKeyword, StringComparison.Ordinal))
            {
                score += 10;
            }
        }

        return score;
    }
}
