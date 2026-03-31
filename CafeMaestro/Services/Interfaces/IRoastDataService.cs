using CafeMaestro.Models;

namespace CafeMaestro.Services;

public interface IRoastDataService
{
    string DataFilePath { get; }
    Task InitializeFromPreferencesAsync(IPreferencesService preferencesService);
    Task<bool> SaveRoastDataAsync(RoastData roastData);
    Task<List<RoastData>> LoadRoastDataAsync();
    Task<List<RoastData>> SearchRoastDataAsync(string beanType = "");
    Task ExportRoastLogAsync(string filePath);
    Task<(int Success, int Failed, List<string> Errors)> ImportRoastsFromCsvAsync(string filePath, Dictionary<string, string> columnMapping);
    Task<int> RemoveDuplicatesAsync();
    Task<List<RoastData>> GetAllRoastsAsync();
    Task<bool> AddRoastAsync(RoastData roast);
    Task<List<RoastData>> GetAllRoastLogsAsync();
    Task<RoastData?> GetRoastLogByIdAsync(Guid id);
    Task<bool> UpdateRoastLogAsync(RoastData updatedRoast);
    Task<bool> DeleteRoastLogAsync(Guid id);
    Task<RoastData?> GetLastRoastForBeanTypeAsync(string beanType);
}
