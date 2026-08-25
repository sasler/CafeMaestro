using CafeMaestro.Models;

namespace CafeMaestro.Services;

public interface IBeanDataService
{
    string DataFilePath { get; }
    Task InitializeFromPreferencesAsync(IPreferencesService preferencesService);
    Task<bool> SaveBeansAsync(List<BeanData> beans);
    Task<bool> AddBeanAsync(BeanData bean);
    Task<bool> UpdateBeanAsync(BeanData bean);
    Task<bool> DeleteBeanAsync(Guid beanId);
    Task<BeanData?> GetBeanByIdAsync(Guid id);

    /// <summary>
    /// Adjusts remaining quantity in kilograms for inventory corrections.
    /// </summary>
    /// <remarks>
    /// Roasting no longer decrements here: <see cref="IRoastSessionService.DropAsync(DropProposal, CancellationToken)"/> moves
    /// inventory inside the same atomic mutation that appends the roast, so a failed write can
    /// never leave beans consumed without a matching log entry.
    /// </remarks>
    Task<bool> UpdateBeanQuantityAsync(Guid beanId, double usedQuantity);
    Task<List<BeanData>> GetAllBeansAsync();
    Task<List<BeanData>> SearchBeansAsync(string searchTerm = "");
    Task<List<BeanData>> GetAvailableBeansAsync();
    Task<List<BeanData>> GetSortedAvailableBeansAsync();
    Task<(int Success, int Failed, List<string> Errors)> ImportBeansFromCsvAsync(string filePath, Dictionary<string, string> columnMapping);
}
