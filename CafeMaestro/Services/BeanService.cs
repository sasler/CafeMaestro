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
    public class BeanService
    {
        private readonly AppDataService _appDataService;
        private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);
        private bool _isInitialized = false;
        private string _currentDataFilePath;

        // Property to get the current data file path
        public string DataFilePath 
        { 
            get => _appDataService.DataFilePath;
        }

        public BeanService(AppDataService appDataService)
        {
            _appDataService = appDataService;
            _currentDataFilePath = _appDataService.DataFilePath;
            
            // Subscribe to path changes from AppDataService
            _appDataService.DataFilePathChanged += OnDataFilePathChanged;
        }
        
        // Handle data file path changes
        private void OnDataFilePathChanged(object? sender, string newPath)
        {
            System.Diagnostics.Debug.WriteLine($"BeanService noticed data file path change to: {newPath}");
            
            // Track current path to help detect changes
            _currentDataFilePath = newPath;
            
            // When the path changes, we should reload data immediately
            // But don't do it in the event handler to avoid deadlocks
            // Instead, queue it on a background thread
            Task.Run(async () => {
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

        // Initialize from preferences - ensure this is called at startup
        public async Task InitializeFromPreferencesAsync(PreferencesService preferencesService)
        {
            await _initLock.WaitAsync();
            
            try
            {
                if (_isInitialized)
                {
                    System.Diagnostics.Debug.WriteLine("BeanService already initialized, skipping");
                    return;
                }
                
                // Force a reload of data
                await _appDataService.ReloadDataAsync();
                _currentDataFilePath = _appDataService.DataFilePath;
                
                System.Diagnostics.Debug.WriteLine($"BeanService initialized with path: {DataFilePath}");
                
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing BeanService from preferences: {ex.Message}");
            }
            finally
            {
                _initLock.Release();
            }
        }

        public async Task<bool> SaveBeansAsync(List<Bean> beans)
        {
            try
            {
                // First verify current path matches expected path
                if (_currentDataFilePath != _appDataService.DataFilePath)
                {
                    System.Diagnostics.Debug.WriteLine($"Path mismatch detected in BeanService.SaveBeansAsync. Expected: {_currentDataFilePath}, Actual: {_appDataService.DataFilePath}");
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
        
        public async Task<bool> AddBeanAsync(Bean bean)
        {
            try
            {
                // Log before adding bean
                System.Diagnostics.Debug.WriteLine($"Adding bean: {bean.CoffeeName}, ID: {bean.Id}, Price: {bean.Price?.ToString() ?? "null"}");
                
                // First verify current path
                if (_currentDataFilePath != _appDataService.DataFilePath)
                {
                    System.Diagnostics.Debug.WriteLine($"Path mismatch detected in BeanService.AddBeanAsync. Expected: {_currentDataFilePath}, Actual: {_appDataService.DataFilePath}");
                    // Synchronize the path
                    _currentDataFilePath = _appDataService.DataFilePath;
                }
                
                // Load full app data
                var appData = await _appDataService.LoadAppDataAsync();
                System.Diagnostics.Debug.WriteLine($"Loaded app data with {appData.Beans.Count} beans");
                
                // Add the bean
                appData.Beans.Add(bean);
                System.Diagnostics.Debug.WriteLine($"Added bean to collection, new count: {appData.Beans.Count}");
                
                // Save updated app data
                bool saveResult = await _appDataService.SaveAppDataAsync(appData);
                System.Diagnostics.Debug.WriteLine($"Save result: {saveResult}");
                
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
        
        public async Task<bool> UpdateBeanAsync(Bean bean)
        {
            try
            {
                // Log before updating bean
                System.Diagnostics.Debug.WriteLine($"Updating bean: {bean.CoffeeName}, ID: {bean.Id}, Price: {bean.Price?.ToString() ?? "null"}");
                
                // First verify current path
                if (_currentDataFilePath != _appDataService.DataFilePath)
                {
                    System.Diagnostics.Debug.WriteLine($"Path mismatch detected in BeanService.UpdateBeanAsync. Expected: {_currentDataFilePath}, Actual: {_appDataService.DataFilePath}");
                    // Synchronize the path
                    _currentDataFilePath = _appDataService.DataFilePath;
                }
                
                // Load full app data
                var appData = await _appDataService.LoadAppDataAsync();
                System.Diagnostics.Debug.WriteLine($"Loaded app data with {appData.Beans.Count} beans for update");
                
                // Find the bean to update
                int index = appData.Beans.FindIndex(b => b.Id == bean.Id);
                System.Diagnostics.Debug.WriteLine($"Bean found at index: {index}");
                
                if (index >= 0)
                {
                    // Replace the old bean with the updated one
                    appData.Beans[index] = bean;
                    System.Diagnostics.Debug.WriteLine($"Replaced bean at index {index}");
                    
                    // Save updated app data
                    bool saveResult = await _appDataService.SaveAppDataAsync(appData);
                    System.Diagnostics.Debug.WriteLine($"Save result: {saveResult}");
                    
                    return saveResult;
                }
                
                // If the bean was not found, add it as a new bean instead of failing
                System.Diagnostics.Debug.WriteLine($"Bean with ID {bean.Id} not found in collection, adding it as new bean");
                appData.Beans.Add(bean);
                
                // Save updated app data with the new bean
                bool addResult = await _appDataService.SaveAppDataAsync(appData);
                System.Diagnostics.Debug.WriteLine($"Added bean since it wasn't found, save result: {addResult}");
                
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
                    System.Diagnostics.Debug.WriteLine($"Path mismatch detected in BeanService.DeleteBeanAsync. Expected: {_currentDataFilePath}, Actual: {_appDataService.DataFilePath}");
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
        
        public async Task<Bean?> GetBeanByIdAsync(Guid id)
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
        
        public async Task<List<Bean>> GetAllBeansAsync()
        {
            try
            {
                // First verify current path
                if (_currentDataFilePath != _appDataService.DataFilePath)
                {
                    System.Diagnostics.Debug.WriteLine($"Path mismatch detected in BeanService.GetAllBeansAsync. Expected: {_currentDataFilePath}, Actual: {_appDataService.DataFilePath}");
                    // Synchronize the path
                    _currentDataFilePath = _appDataService.DataFilePath;
                }
                
                // Load full app data from current (correct) path
                var appData = await _appDataService.LoadAppDataAsync();
                
                // Log loaded data
                System.Diagnostics.Debug.WriteLine($"BeanService loaded {appData.Beans?.Count ?? 0} beans from {_currentDataFilePath}");
                
                // Log details of each bean for debugging
                if (appData.Beans != null && appData.Beans.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine("Beans in collection:");
                    foreach (var bean in appData.Beans)
                    {
                        System.Diagnostics.Debug.WriteLine($"  Bean ID: {bean.Id}, Name: {bean.CoffeeName}, Country: {bean.Country}");
                    }
                }
                
                return appData.Beans ?? new List<Bean>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading beans: {ex.Message}");
                return new List<Bean>();
            }
        }
        
        public async Task<List<Bean>> SearchBeansAsync(string searchTerm = "")
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
        
        public async Task<List<Bean>> GetAvailableBeansAsync()
        {
            var allBeans = await GetAllBeansAsync();
            return allBeans.Where(b => b.RemainingQuantity > 0).ToList();
        }
    }
}