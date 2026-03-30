using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace CafeMaestro.Models
{
    public class BeanData
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime PurchaseDate { get; set; } = DateTime.Now;
        public string Country { get; set; } = "";
        public string CoffeeName { get; set; } = "";
        public string Variety { get; set; } = "";
        public string Process { get; set; } = "";
        public string Notes { get; set; } = "";
        public double Quantity { get; set; } // in kg
        public decimal? Price { get; set; } // Optional price
        public string Link { get; set; } = "";
        public double RemainingQuantity { get; set; } // track how much is left

        [JsonIgnore]
        public string DisplayName => $"{Country} - {CoffeeName} ({Variety})";

        [JsonIgnore]
        public string ProcessAndVarietyDisplay => $"{Process} | {Variety}";

        [JsonIgnore]
        public string QuantityDisplay => $"{RemainingQuantity:F2}kg / {Quantity:F2}kg";

        [JsonIgnore]
        public string PriceDisplay => Price.HasValue ? $"${Price:F2}" : "Price not set";
        
        [JsonIgnore]
        public bool IsOutOfStock => RemainingQuantity <= 0;

        [JsonIgnore]
        public bool IsValid => !Validate().Any();

        public BeanData()
        {
        }

        public List<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(Country))
            {
                errors.Add("Country must not be empty or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(CoffeeName))
            {
                errors.Add("Coffee name must not be empty or whitespace.");
            }

            if (Quantity < 0)
            {
                errors.Add("Quantity must be greater than or equal to 0.");
            }

            if (RemainingQuantity < 0)
            {
                errors.Add("RemainingQuantity must be greater than or equal to 0.");
            }

            if (RemainingQuantity > Quantity)
            {
                errors.Add("RemainingQuantity must be less than or equal to Quantity.");
            }

            if (Price is < 0)
            {
                errors.Add("Price must be greater than or equal to 0.");
            }

            return errors;
        }

        // Method to use some of the beans for a roast
        public bool UseQuantity(double amount)
        {
            if (amount <= 0 || amount > RemainingQuantity)
                return false;
                
            RemainingQuantity -= amount;
            return true;
        }
    }
}
