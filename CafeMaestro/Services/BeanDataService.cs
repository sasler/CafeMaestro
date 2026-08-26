using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CafeMaestro.Models;

namespace CafeMaestro.Services
{
    public class BeanDataService : IBeanDataService, IDisposable
    {
        private readonly IAppDataService _appDataService;
        private bool _isDisposed;
        private string _currentDataFilePath;

        // Property to get the current data file path
        public string DataFilePath
        {
            get => _appDataService.DataFilePath;
        }

        public BeanDataService(IAppDataService appDataService)
        {
            _appDataService = appDataService;
            _currentDataFilePath = _appDataService.DataFilePath;

            // Subscribe to path changes from AppDataService
            _appDataService.DataFilePathChanged += OnDataFilePathChanged;
        }

        // Handle data file path changes
        private void OnDataFilePathChanged(object? sender, string newPath)
        {
            // Track current path to help detect changes
            _currentDataFilePath = newPath;

            // When the path changes, we should reload data immediately
            // But don't do it in the event handler to avoid deadlocks
            // Instead, queue it on a background thread
            Task.Run(async () =>
            {
                try
                {
                    await _appDataService.ReloadDataAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error reloading data after path change in BeanService: {ex.Message}");
                }
            });
        }

        public async Task<bool> SaveBeansAsync(List<BeanData> beans)
        {
            try
            {
                // First verify current path matches expected path
                if (_currentDataFilePath != _appDataService.DataFilePath)
                {
                    // Synchronize the path
                    _currentDataFilePath = _appDataService.DataFilePath;
                }

                // Load full app data
                var appData = await _appDataService.LoadAppDataAsync();

                // Replace beans collection
                appData.Beans = beans;

                // Save updated app data
                return await _appDataService.SaveAppDataAsync(appData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving beans: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> AddBeanAsync(BeanData bean)
        {
            try
            {
                // First verify current path
                if (_currentDataFilePath != _appDataService.DataFilePath)
                {
                    // Synchronize the path
                    _currentDataFilePath = _appDataService.DataFilePath;
                }

                // Load full app data
                var appData = await _appDataService.LoadAppDataAsync();

                // Add the bean
                appData.Beans.Add(bean);

                // Save updated app data
                bool saveResult = await _appDataService.SaveAppDataAsync(appData);

                return saveResult;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding bean: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Exception type: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        public async Task<bool> UpdateBeanAsync(BeanData bean)
        {
            try
            {

                // First verify current path
                if (_currentDataFilePath != _appDataService.DataFilePath)
                {
                    // Synchronize the path
                    _currentDataFilePath = _appDataService.DataFilePath;
                }

                // Load full app data
                var appData = await _appDataService.LoadAppDataAsync();

                // Find the bean to update
                int index = appData.Beans.FindIndex(b => b.Id == bean.Id);

                if (index >= 0)
                {
                    // Replace the old bean with the updated one
                    appData.Beans[index] = bean;

                    // Save updated app data
                    bool saveResult = await _appDataService.SaveAppDataAsync(appData);
                    return saveResult;
                }

                // If the bean was not found, add it as a new bean instead of failing
                appData.Beans.Add(bean);

                // Save updated app data with the new bean
                bool addResult = await _appDataService.SaveAppDataAsync(appData);

                return addResult;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating bean: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Exception type: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        public async Task<bool> DeleteBeanAsync(Guid beanId)
        {
            try
            {
                // First verify current path
                if (_currentDataFilePath != _appDataService.DataFilePath)
                {
                    // Synchronize the path
                    _currentDataFilePath = _appDataService.DataFilePath;
                }

                // Load full app data
                var appData = await _appDataService.LoadAppDataAsync();

                // Find and remove the bean
                int index = appData.Beans.FindIndex(b => b.Id == beanId);

                if (index >= 0)
                {
                    // Remove the bean
                    appData.Beans.RemoveAt(index);

                    // Save updated app data
                    return await _appDataService.SaveAppDataAsync(appData);
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting bean: {ex.Message}");
                return false;
            }
        }

        public async Task<BeanData?> GetBeanByIdAsync(Guid id)
        {
            var allBeans = await GetAllBeansAsync();
            return allBeans.FirstOrDefault(b => b.Id == id);
        }

        public async Task<bool> UpdateBeanQuantityAsync(Guid beanId, double usedQuantity)
        {
            try
            {
                // Get the bean
                var bean = await GetBeanByIdAsync(beanId);

                if (bean == null)
                    return false;

                // Calculate new remaining quantity
                double newQuantity = bean.RemainingQuantity - usedQuantity;

                // Ensure we don't go below zero
                bean.RemainingQuantity = Math.Max(0, newQuantity);

                // Update the bean
                return await UpdateBeanAsync(bean);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating bean quantity: {ex.Message}");
                return false;
            }
        }

        public async Task<List<BeanData>> GetAllBeansAsync()
        {
            try
            {
                // First verify current path
                if (_currentDataFilePath != _appDataService.DataFilePath)
                {
                    // Synchronize the path
                    _currentDataFilePath = _appDataService.DataFilePath;
                }

                // Load full app data from current (correct) path
                var appData = await _appDataService.LoadAppDataAsync();

                return appData.Beans ?? new List<BeanData>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading beans: {ex.Message}");
                throw;
            }
        }

        public async Task<List<BeanData>> SearchBeansAsync(string searchTerm = "")
        {
            var beans = await GetAllBeansAsync();

            if (string.IsNullOrWhiteSpace(searchTerm))
                return beans;

            return beans.FindAll(b =>
                b.Country.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                b.CoffeeName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                b.Variety.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                b.Process.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<List<BeanData>> GetAvailableBeansAsync()
        {
            try
            {
                // First verify current path matches what we expect
                if (_currentDataFilePath != _appDataService.DataFilePath)
                {
                    // Synchronize the path
                    _currentDataFilePath = _appDataService.DataFilePath;
                }

                // Load app data directly from file to ensure freshness
                var appData = await _appDataService.LoadAppDataAsync();

                // Check if beans collection is null or empty
                if (appData.Beans == null || appData.Beans.Count == 0)
                {
                    return new List<BeanData>();
                }

                // Get beans with remaining quantity
                var availableBeans = appData.Beans.Where(b => b.RemainingQuantity > 0).ToList();

                return availableBeans;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting available beans: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return new List<BeanData>();
            }
        }

        public async Task<List<BeanData>> GetSortedAvailableBeansAsync()
        {
            try
            {

                // First verify current path matches what we expect
                if (_currentDataFilePath != _appDataService.DataFilePath)
                {
                    // Synchronize the path
                    _currentDataFilePath = _appDataService.DataFilePath;
                }

                // Load app data directly from file to ensure freshness
                var appData = await _appDataService.LoadAppDataAsync();

                // Check if beans collection is null or empty
                if (appData.Beans == null || appData.Beans.Count == 0)
                {
                    return new List<BeanData>();
                }

                // Inventory is advisory during roast setup. Keep every bean selectable so stale or
                // short quantity bookkeeping can surface as a non-blocking warning instead of a gate.
                var sortedBeans = appData.Beans
                    .OrderByDescending(b => b.PurchaseDate)
                    .ThenBy(b => b.DisplayName)
                    .ToList();

                return sortedBeans;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting sorted available beans: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return new List<BeanData>();
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _appDataService.DataFilePathChanged -= OnDataFilePathChanged;
        }
    }
}
