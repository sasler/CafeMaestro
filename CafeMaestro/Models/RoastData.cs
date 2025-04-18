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

        [JsonIgnore]
        public double WeightLossPercentage => Math.Round(((BatchWeight - FinalWeight) / BatchWeight) * 100, 2);

        [JsonIgnore]
        public string FormattedTime => $"{RoastMinutes:D2}:{RoastSeconds:D2}";

        [JsonIgnore]
        public int TotalSeconds => (RoastMinutes * 60) + RoastSeconds; // Add TotalSeconds property

        [JsonIgnore]
        public string Summary => $"{RoastLevelName} roast of {BeanType} at {Temperature}°C for {FormattedTime}";
    }
}