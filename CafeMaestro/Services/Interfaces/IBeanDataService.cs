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
    Task<bool> UpdateBeanQuantityAsync(Guid beanId, double usedQuantity);
    Task<List<BeanData>> GetAllBeansAsync();
    Task<List<BeanData>> SearchBeansAsync(string searchTerm = "");
    Task<List<BeanData>> GetAvailableBeansAsync();
    Task<List<BeanData>> GetSortedAvailableBeansAsync();
    Task<List<Dictionary<string, string>>> ReadCsvContentAsync(string filePath, int maxRows = 100);
    Task<(int Success, int Failed, List<string> Errors)> ImportBeansFromCsvAsync(string filePath, Dictionary<string, string> columnMapping);
}
