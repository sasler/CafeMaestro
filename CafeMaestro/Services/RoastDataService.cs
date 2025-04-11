using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CafeMaestro.Models;

namespace CafeMaestro.Services
{
    public class RoastDataService
    {
        private readonly string _folderPath;
        private readonly string _fileName = "roast_log.json";
        private readonly JsonSerializerOptions _jsonOptions;

        public RoastDataService()
        {
            _folderPath = Path.Combine(FileSystem.AppDataDirectory, "RoastData");
            
            // Create the directory if it doesn't exist
            if (!Directory.Exists(_folderPath))
                Directory.CreateDirectory(_folderPath);
                
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        public async Task<bool> SaveRoastDataAsync(RoastData roastData)
        {
            try
            {
                // Load existing data first
                var existingData = await LoadRoastDataAsync();
                
                // Add the new data
                existingData.Add(roastData);
                
                // Save everything back to the file
                string filePath = Path.Combine(_folderPath, _fileName);
                string jsonString = JsonSerializer.Serialize(existingData, _jsonOptions);
                await File.WriteAllTextAsync(filePath, jsonString);
                
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving roast data: {ex.Message}");
                return false;
            }
        }
        
        public async Task<List<RoastData>> LoadRoastDataAsync()
        {
            try
            {
                string filePath = Path.Combine(_folderPath, _fileName);
                
                if (!File.Exists(filePath))
                    return new List<RoastData>();
                    
                string jsonString = await File.ReadAllTextAsync(filePath);
                var roastDataList = JsonSerializer.Deserialize<List<RoastData>>(jsonString, _jsonOptions);
                
                return roastDataList ?? new List<RoastData>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading roast data: {ex.Message}");
                return new List<RoastData>();
            }
        }

        public async Task<List<RoastData>> SearchRoastDataAsync(string beanType = "")
        {
            var allData = await LoadRoastDataAsync();
            
            if (string.IsNullOrWhiteSpace(beanType))
                return allData;
                
            return allData.FindAll(r => r.BeanType.Contains(beanType, StringComparison.OrdinalIgnoreCase));
        }

        public async Task ExportRoastLogAsync(string filePath)
        {
            try
            {
                var allData = await LoadRoastDataAsync();
                var csv = new System.Text.StringBuilder();
                
                // Add header
                csv.AppendLine("Date,Bean Type,Temperature,Batch Weight,Final Weight,Weight Loss %,Roast Time,Roast Level,Notes");
                
                // Add data rows
                foreach (var roast in allData)
                {
                    csv.AppendLine($"{roast.RoastDate:yyyy-MM-dd HH:mm}," +
                                  $"\"{roast.BeanType}\"," +
                                  $"{roast.Temperature}," +
                                  $"{roast.BatchWeight}," +
                                  $"{roast.FinalWeight}," +
                                  $"{roast.WeightLossPercentage}," +
                                  $"{roast.FormattedTime}," +
                                  $"{roast.RoastLevel}," +
                                  $"\"{roast.Notes}\"");
                }
                
                // Write to file
                await File.WriteAllTextAsync(filePath, csv.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error exporting roast log: {ex.Message}");
                throw;
            }
        }
    }
}