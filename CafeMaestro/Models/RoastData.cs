using System;
using System.Text.Json.Serialization;

namespace CafeMaestro.Models
{
    public class RoastData
    {
        public Guid Id { get; set; } = Guid.NewGuid(); // Add Id property
        public string BeanType { get; set; } = "";
        public double Temperature { get; set; }
        public double BatchWeight { get; set; }
        public double FinalWeight { get; set; }
        public int RoastMinutes { get; set; }
        public int RoastSeconds { get; set; }
        public DateTime RoastDate { get; set; }
        public string Notes { get; set; } = "";
        public string RoastLevelName { get; set; } = ""; // Store the calculated roast level name
        
        // First Crack tracking properties (removed FirstCrackMarked)
        public int? FirstCrackMinutes { get; set; } = null;
        public int? FirstCrackSeconds { get; set; } = null;

        [JsonIgnore]
        public double WeightLossPercentage => Math.Round(((BatchWeight - FinalWeight) / BatchWeight) * 100, 2);

        [JsonIgnore]
        public string FormattedTime => $"{RoastMinutes:D2}:{RoastSeconds:D2}";

        [JsonIgnore]
        public int TotalSeconds => (RoastMinutes * 60) + RoastSeconds; // Add TotalSeconds property

        [JsonIgnore]
        public string FirstCrackTime => FirstCrackSeconds.HasValue 
            ? $"{FirstCrackMinutes:D2}:{FirstCrackSeconds:D2}" 
            : "Not marked";

        [JsonIgnore]
        public double DevelopmentTimeRatio => FirstCrackSeconds.HasValue 
            ? Math.Round((double)(TotalSeconds - ((FirstCrackMinutes.GetValueOrDefault() * 60) + FirstCrackSeconds.GetValueOrDefault())) / TotalSeconds * 100, 1)
            : 0;

        [JsonIgnore]
        public string Summary => $"{RoastLevelName} roast of {BeanType} at {Temperature}°C for {FormattedTime}";
    }
}