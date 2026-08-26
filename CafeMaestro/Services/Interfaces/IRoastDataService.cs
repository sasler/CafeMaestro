using CafeMaestro.Models;

namespace CafeMaestro.Services;

/// <summary>
/// Roast log queries, focused editing, and export. CSV import belongs to <see cref="IImportService"/>.
/// </summary>
/// <remarks>
/// Roast workflow transitions — start, drop, weigh-in, unweighed, discard — belong to
/// <see cref="IRoastSessionService"/>, which owns them as single atomic mutations that also
/// move inventory. This service intentionally keeps only focused edits and read/export access;
/// new records enter through the session domain or <see cref="IImportService"/>.
/// </remarks>
public interface IRoastDataService
{
    string DataFilePath { get; }
    Task ExportRoastLogAsync(Stream destination, CancellationToken cancellationToken = default);
    Task<RoastData?> GetRoastLogByIdAsync(Guid id);
    Task<bool> UpdateRoastLogAsync(RoastData updatedRoast);
    Task<bool> DeleteRoastLogAsync(Guid id);
    /// <summary>Returns the newest roast attributable to the stable bean identity.</summary>
    Task<RoastData?> GetLastRoastForBeanAsync(Guid beanId);
}
