using CafeMaestro.Models;

namespace CafeMaestro.Services;

/// <summary>
/// Roast log queries, import, and export.
/// </summary>
/// <remarks>
/// Roast workflow transitions — start, drop, weigh-in, unweighed, discard — belong to
/// <see cref="IRoastSessionService"/>, which owns them as single atomic mutations that also
/// move inventory. The append/update members here remain for import, manual log editing, and
/// backward compatibility; they do not touch bean quantity or the active session.
/// </remarks>
public interface IRoastDataService
{
    string DataFilePath { get; }
    Task InitializeFromPreferencesAsync(IPreferencesService preferencesService);

    /// <summary>
    /// Appends a roast without session semantics. Roasts produced by the console must go through
    /// <see cref="IRoastSessionService.DropAsync"/> instead, so inventory moves exactly once.
    /// </summary>
    Task<bool> SaveRoastDataAsync(RoastData roastData);
    Task<List<RoastData>> LoadRoastDataAsync();
    Task<List<RoastData>> SearchRoastDataAsync(string beanType = "");
    Task ExportRoastLogAsync(Stream destination, CancellationToken cancellationToken = default);
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
