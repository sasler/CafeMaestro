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

        [JsonIgnore]
        public double WeightLossPercentage => Math.Round(((BatchWeight - FinalWeight) / BatchWeight) * 100, 2);

        [JsonIgnore]
        public string FormattedTime => $"{RoastMinutes:D2}:{RoastSeconds:D2}";

        [JsonIgnore]
        public int TotalSeconds => (RoastMinutes * 60) + RoastSeconds; // Add TotalSeconds property

        [JsonIgnore]
        public string RoastLevel
        {
            get
            {
                if (WeightLossPercentage < 12.0)
                    return "Light";
                else if (WeightLossPercentage < 14.0)
                    return "Medium-Light";
                else if (WeightLossPercentage < 16.0)
                    return "Medium";
                else if (WeightLossPercentage < 18.0)
                    return "Medium-Dark";
                else
                    return "Dark";
            }
        }

        [JsonIgnore]
        public string Summary => $"{RoastLevel} roast of {BeanType} at {Temperature}°C for {FormattedTime}";
    }
}