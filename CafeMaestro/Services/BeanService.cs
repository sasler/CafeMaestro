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

        public BeanService(AppDataService appDataService)
        {
            _appDataService = appDataService;
        }

        public async Task<bool> SaveBeansAsync(List<Bean> beans)
        {
            try
            {
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
                // Load full app data
                var appData = await _appDataService.LoadAppDataAsync();
                
                // Add the new bean
                appData.Beans.Add(bean);
                
                // Save updated app data
                return await _appDataService.SaveAppDataAsync(appData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding bean: {ex.Message}");
                return false;
            }
        }
        
        public async Task<bool> UpdateBeanAsync(Bean bean)
        {
            try
            {
                // Load full app data
                var appData = await _appDataService.LoadAppDataAsync();
                
                // Find and update the bean
                var existingIndex = appData.Beans.FindIndex(b => b.Id == bean.Id);
                if (existingIndex == -1)
                    return false;
                
                appData.Beans[existingIndex] = bean;
                
                // Save updated app data
                return await _appDataService.SaveAppDataAsync(appData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating bean: {ex.Message}");
                return false;
            }
        }
        
        public async Task<bool> DeleteBeanAsync(Guid beanId)
        {
            try
            {
                // Load full app data
                var appData = await _appDataService.LoadAppDataAsync();
                
                // Remove the bean
                var removedCount = appData.Beans.RemoveAll(b => b.Id == beanId);
                if (removedCount == 0)
                    return false;
                
                // Save updated app data
                return await _appDataService.SaveAppDataAsync(appData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting bean: {ex.Message}");
                return false;
            }
        }
        
        public async Task<Bean?> GetBeanByIdAsync(Guid beanId)
        {
            try
            {
                var appData = await _appDataService.LoadAppDataAsync();
                return appData.Beans.FirstOrDefault(b => b.Id == beanId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting bean: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> UpdateBeanQuantityAsync(Guid beanId, double usedAmount)
        {
            try
            {
                // Load full app data
                var appData = await _appDataService.LoadAppDataAsync();
                
                // Find the bean
                var bean = appData.Beans.FirstOrDefault(b => b.Id == beanId);
                if (bean == null)
                    return false;
                
                // Update quantity
                if (bean.RemainingQuantity < usedAmount)
                    return false;
                    
                bean.RemainingQuantity -= usedAmount;
                
                // Save all beans
                return await _appDataService.SaveAppDataAsync(appData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating bean quantity: {ex.Message}");
                return false;
            }
        }
        
        public async Task<List<Bean>> LoadBeansAsync()
        {
            try
            {
                var appData = await _appDataService.LoadAppDataAsync();
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
            var beans = await LoadBeansAsync();
            
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
            var beans = await LoadBeansAsync();
            return beans.FindAll(b => b.RemainingQuantity > 0);
        }
    }
}