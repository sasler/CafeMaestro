using System;
using System.Collections.Generic;
using System.Linq;
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
        public bool HasFinalWeight => FinalWeight > 0;

        [JsonIgnore]
        public double WeightLossPercentage => HasFinalWeight && BatchWeight > 0
            ? Math.Round(((BatchWeight - FinalWeight) / BatchWeight) * 100, 2)
            : 0;

        [JsonIgnore]
        public string WeightLossDisplay => HasFinalWeight
            ? $"{WeightLossPercentage:F1}%"
            : "Pending";

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
        public string Summary => HasFinalWeight
            ? $"{RoastLevelName} roast of {BeanType} at {Temperature}°C for {FormattedTime}"
            : $"Pending roast of {BeanType} at {Temperature}°C for {FormattedTime}";

        [JsonIgnore]
        public bool IsValid => !Validate().Any();

        public List<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(BeanType))
            {
                errors.Add("BeanType must not be empty.");
            }

            if (BatchWeight <= 0)
            {
                errors.Add("BatchWeight must be greater than 0.");
            }

            if (FinalWeight < 0)
            {
                errors.Add("FinalWeight must be greater than or equal to 0.");
            }

            if (FinalWeight > BatchWeight)
            {
                errors.Add("FinalWeight must be less than or equal to BatchWeight.");
            }

            if (Temperature <= 0 || Temperature > 500)
            {
                errors.Add("Temperature must be greater than 0 and less than or equal to 500.");
            }

            if (RoastMinutes < 0)
            {
                errors.Add("RoastMinutes must be greater than or equal to 0.");
            }

            if (RoastSeconds < 0 || RoastSeconds >= 60)
            {
                errors.Add("RoastSeconds must be between 0 and 59.");
            }

            var hasFirstCrackMinutes = FirstCrackMinutes.HasValue;
            var hasFirstCrackSeconds = FirstCrackSeconds.HasValue;

            if (hasFirstCrackMinutes != hasFirstCrackSeconds)
            {
                errors.Add("First crack time requires both minutes and seconds.");
            }
            else if (hasFirstCrackMinutes && hasFirstCrackSeconds)
            {
                if (FirstCrackMinutes < 0)
                {
                    errors.Add("First crack minutes must be greater than or equal to 0.");
                }

                if (FirstCrackSeconds < 0 || FirstCrackSeconds >= 60)
                {
                    errors.Add("First crack seconds must be between 0 and 59.");
                }

                if (errors.All(error => !error.Contains("First crack")))
                {
                    var totalRoastSeconds = (RoastMinutes * 60) + RoastSeconds;
                    var firstCrackTotalSeconds = (FirstCrackMinutes.GetValueOrDefault() * 60) + FirstCrackSeconds.GetValueOrDefault();

                    if (firstCrackTotalSeconds > totalRoastSeconds)
                    {
                        errors.Add("First crack time must be within the total roast time.");
                    }
                }
            }

            return errors;
        }
    }
}
