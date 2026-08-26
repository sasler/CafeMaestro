using System.Text;

namespace CafeMaestro.Services;

/// <summary>
/// RFC 4180-style CSV reading. Parsing is record-aware rather than line-aware: a quoted field may
/// contain commas, escaped quotes (<c>""</c>), and newlines, so one logical record can span several
/// physical lines. CafeMaestro's own roast-log export quotes every note, so a note with a line
/// break produces exactly that shape.
/// </summary>
public class CsvParserService : ICsvParserService
{
    public async Task<List<string>> GetCsvHeadersAsync(string filePath)
    {
        try
        {
            EnsureFileExists(filePath);

            return await Task.Run(() =>
            {
                List<List<string>> records = ReadRecords(filePath);
                return records.Count == 0
                    ? []
                    : records[0].Select(header => header.Trim()).ToList();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error reading CSV headers: {ex.Message}");
            throw;
        }
    }

    public async Task<List<Dictionary<string, string>>> ReadCsvContentAsync(string filePath, int maxRows = 100)
    {
        try
        {
            EnsureFileExists(filePath);

            return await Task.Run(() =>
            {
                var result = new List<Dictionary<string, string>>();
                List<List<string>> records = ReadRecords(filePath);

                if (records.Count < 2)
                {
                    return result;
                }

                string[] headers = records[0].Select(header => header.Trim()).ToArray();

                for (int i = 1; i < records.Count && result.Count < maxRows; i++)
                {
                    List<string> values = records[i];
                    var rowData = new Dictionary<string, string>();

                    for (int column = 0; column < Math.Min(headers.Length, values.Count); column++)
                    {
                        rowData[headers[column]] = values[column];
                    }

                    result.Add(rowData);
                }

                return result;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error reading CSV content: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Exception details: {ex}");
            throw;
        }
    }

    private static void EnsureFileExists(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            throw new FileNotFoundException("CSV file not found", filePath);
        }
    }

    private static List<List<string>> ReadRecords(string filePath)
    {
        return ParseRecords(File.ReadAllText(filePath));
    }

    /// <summary>
    /// Splits CSV text into logical records, dropping blank records and <c>//</c> comment lines.
    /// Never throws on malformed content: an unterminated quote simply ends with the file, so a
    /// single bad record can never stop the rest of the file from reaching review.
    /// </summary>
    internal static List<List<string>> ParseRecords(string text)
    {
        var records = new List<List<string>>();

        if (string.IsNullOrEmpty(text))
        {
            return records;
        }

        var record = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;
        bool fieldWasQuoted = false;

        void EndField()
        {
            record.Add(fieldWasQuoted ? field.ToString() : field.ToString().Trim());
            field.Clear();
            fieldWasQuoted = false;
        }

        void EndRecord()
        {
            EndField();

            if (IsRetainedRecord(record))
            {
                records.Add([.. record]);
            }

            record.Clear();
        }

        for (int i = 0; i < text.Length; i++)
        {
            char character = text[i];

            if (inQuotes)
            {
                if (character != '"')
                {
                    field.Append(character);
                    continue;
                }

                // A doubled quote inside a quoted field is one literal quote.
                if (i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                    continue;
                }

                inQuotes = false;
                continue;
            }

            switch (character)
            {
                case '"':
                    inQuotes = true;
                    fieldWasQuoted = true;
                    continue;
                case ',':
                    EndField();
                    continue;
                case '\r':
                    // Swallow CR so CRLF and lone CR both end exactly one record.
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }

                    EndRecord();
                    continue;
                case '\n':
                    EndRecord();
                    continue;
                default:
                    field.Append(character);
                    continue;
            }
        }

        // A file that does not end with a newline still has a final record.
        if (field.Length > 0 || record.Count > 0 || inQuotes)
        {
            EndRecord();
        }

        return records;
    }

    private static bool IsRetainedRecord(List<string> record)
    {
        if (record.All(string.IsNullOrWhiteSpace))
        {
            return false;
        }

        return !record[0].TrimStart().StartsWith("//", StringComparison.Ordinal);
    }
}
