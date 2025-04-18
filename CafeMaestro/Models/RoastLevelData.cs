using System;
using System.Text.Json.Serialization;

namespace CafeMaestro.Models
{
    public class RoastLevelData
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public double MinWeightLossPercentage { get; set; }
        public double MaxWeightLossPercentage { get; set; }

        [JsonIgnore]
        public string DisplayName => $"{Name} ({MinWeightLossPercentage}% - {MaxWeightLossPercentage}%)";

        // Default constructor
        public RoastLevelData()
        {
        }

        // Constructor with parameters
        public RoastLevelData(string name, double minWeightLoss, double maxWeightLoss)
        {
            Name = name;
            MinWeightLossPercentage = minWeightLoss;
            MaxWeightLossPercentage = maxWeightLoss;
        }
    }
}