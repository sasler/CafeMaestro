using System;
using System.Collections.Generic;
using System.Linq;
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

        [JsonIgnore]
        public bool IsValid => !Validate().Any();

        public RoastLevelData()
        {
        }

        public RoastLevelData(string name, double minWeightLoss, double maxWeightLoss)
        {
            Name = name;
            MinWeightLossPercentage = minWeightLoss;
            MaxWeightLossPercentage = maxWeightLoss;
        }

        public List<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(Name))
            {
                errors.Add("Name must not be empty.");
            }

            if (MinWeightLossPercentage < 0)
            {
                errors.Add("MinWeightLossPercentage must be greater than or equal to 0.");
            }

            if (MinWeightLossPercentage > 100)
            {
                errors.Add("MinWeightLossPercentage must be less than or equal to 100.");
            }

            if (MaxWeightLossPercentage < MinWeightLossPercentage)
            {
                errors.Add("MaxWeightLossPercentage must be greater than or equal to MinWeightLossPercentage.");
            }

            if (MaxWeightLossPercentage > 100)
            {
                errors.Add("MaxWeightLossPercentage must be less than or equal to 100.");
            }

            return errors;
        }
    }
}
