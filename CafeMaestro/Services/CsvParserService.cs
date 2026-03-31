using System.Text;

namespace CafeMaestro.Services;

public class CsvParserService : ICsvParserService
{
    public async Task<List<string>> GetCsvHeadersAsync(string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                throw new FileNotFoundException("CSV file not found", filePath);
            }

            return await Task.Run(() =>
            {
                var lines = File.ReadAllLines(filePath);
                int headerLineIndex = 0;

                while (headerLineIndex < lines.Length &&
                       (string.IsNullOrWhiteSpace(lines[headerLineIndex]) ||
                        lines[headerLineIndex].TrimStart().StartsWith("//")))
                {
                    headerLineIndex++;
                }

                if (headerLineIndex >= lines.Length)
                {
                    return new List<string>();
                }

                string headerLine = lines[headerLineIndex];
                return headerLine.Split(',').Select(h => h.Trim()).ToList();
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
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                throw new FileNotFoundException("CSV file not found", filePath);
            }

            return await Task.Run(() =>
            {
                var result = new List<Dictionary<string, string>>();
                string[] lines = File.ReadAllLines(filePath);

                int headerLineIndex = 0;
                while (headerLineIndex < lines.Length &&
                       (string.IsNullOrWhiteSpace(lines[headerLineIndex]) ||
                        lines[headerLineIndex].TrimStart().StartsWith("//")))
                {
                    headerLineIndex++;
                }

                if (headerLineIndex >= lines.Length - 1)
                {
                    return result;
                }

                string[] headers = lines[headerLineIndex].Split(',').Select(h => h.Trim()).ToArray();
                int rowsProcessed = 0;

                for (int i = headerLineIndex + 1; i < lines.Length && rowsProcessed < maxRows; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    List<string> values = SplitCsvLine(line);
                    var rowData = new Dictionary<string, string>();

                    for (int j = 0; j < Math.Min(headers.Length, values.Count); j++)
                    {
                        rowData[headers[j]] = values[j];
                    }

                    result.Add(rowData);
                    rowsProcessed++;
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

    private List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var currentValue = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
                currentValue.Append(c);
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(CleanCsvValue(currentValue.ToString()));
                currentValue.Clear();
            }
            else
            {
                currentValue.Append(c);
            }
        }

        result.Add(CleanCsvValue(currentValue.ToString()));
        return result;
    }

    private string CleanCsvValue(string value)
    {
        value = value.Trim();

        if (value.StartsWith("\"") && value.EndsWith("\""))
        {
            value = value.Substring(1, value.Length - 2);
        }

        return value.Replace("\"\"", "\"");
    }
}
