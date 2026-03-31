using CafeMaestro.Models;

namespace CafeMaestro.Services;

public interface IRoastLevelService
{
    string DataFilePath { get; }
    Task InitializeFromPreferencesAsync(IPreferencesService preferencesService);
    Task<string> GetRoastLevelNameAsync(double weightLossPercentage);
    Task<List<RoastLevelData>> GetRoastLevelsAsync();
    Task<bool> SaveRoastLevelsAsync(List<RoastLevelData> levels);
    Task<bool> AddRoastLevelAsync(RoastLevelData level);
    Task<bool> DeleteRoastLevelAsync(Guid id);
    Task<bool> UpdateRoastLevelAsync(RoastLevelData updatedLevel);
    Task<RoastLevelData?> GetRoastLevelByIdAsync(Guid id);
}
