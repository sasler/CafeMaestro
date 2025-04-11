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
        private readonly string _folderPath;
        private readonly string _fileName = "bean_inventory.json";
        private readonly JsonSerializerOptions _jsonOptions;

        public BeanService()
        {
            _folderPath = Path.Combine(FileSystem.AppDataDirectory, "BeanData");
            
            // Create the directory if it doesn't exist
            if (!Directory.Exists(_folderPath))
                Directory.CreateDirectory(_folderPath);
                
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        public async Task<bool> SaveBeansAsync(List<Bean> beans)
        {
            try
            {
                string filePath = Path.Combine(_folderPath, _fileName);
                string jsonString = JsonSerializer.Serialize(beans, _jsonOptions);
                await File.WriteAllTextAsync(filePath, jsonString);
                
                return true;
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
                // Load existing beans
                var beans = await LoadBeansAsync();
                
                // Add the new bean
                beans.Add(bean);
                
                // Save all beans
                return await SaveBeansAsync(beans);
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
                // Load existing beans
                var beans = await LoadBeansAsync();
                
                // Find and update the bean
                var existingIndex = beans.FindIndex(b => b.Id == bean.Id);
                if (existingIndex == -1)
                    return false;
                
                beans[existingIndex] = bean;
                
                // Save all beans
                return await SaveBeansAsync(beans);
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
                // Load existing beans
                var beans = await LoadBeansAsync();
                
                // Remove the bean
                var removedCount = beans.RemoveAll(b => b.Id == beanId);
                if (removedCount == 0)
                    return false;
                
                // Save all beans
                return await SaveBeansAsync(beans);
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
                var beans = await LoadBeansAsync();
                return beans.FirstOrDefault(b => b.Id == beanId);
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
                // Load existing beans
                var beans = await LoadBeansAsync();
                
                // Find the bean
                var bean = beans.FirstOrDefault(b => b.Id == beanId);
                if (bean == null)
                    return false;
                
                // Update quantity
                if (!bean.UseQuantity(usedAmount))
                    return false;
                
                // Save all beans
                return await SaveBeansAsync(beans);
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
                string filePath = Path.Combine(_folderPath, _fileName);
                
                if (!File.Exists(filePath))
                    return new List<Bean>();
                    
                string jsonString = await File.ReadAllTextAsync(filePath);
                var beans = JsonSerializer.Deserialize<List<Bean>>(jsonString, _jsonOptions);
                
                return beans ?? new List<Bean>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading beans: {ex.Message}");
                return new List<Bean>();
            }
        }
        
        public async Task<List<Bean>> SearchBeansAsync(string searchTerm = "")
        {
            var allBeans = await LoadBeansAsync();
            
            if (string.IsNullOrWhiteSpace(searchTerm))
                return allBeans;
                
            return allBeans.Where(b => 
                b.Country.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                b.CoffeeName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                b.Variety.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                b.Process.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                b.Notes.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }
        
        public async Task<List<Bean>> GetAvailableBeansAsync()
        {
            var allBeans = await LoadBeansAsync();
            return allBeans.Where(b => b.RemainingQuantity > 0).ToList();
        }
    }
}