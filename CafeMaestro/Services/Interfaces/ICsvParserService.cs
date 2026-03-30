namespace CafeMaestro.Services;

public interface ICsvParserService
{
    Task<List<string>> GetCsvHeadersAsync(string filePath);
    Task<List<Dictionary<string, string>>> ReadCsvContentAsync(string filePath, int maxRows = 100);
}
