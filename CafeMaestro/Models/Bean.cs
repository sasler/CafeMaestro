using System;
using System.Text.Json.Serialization;

namespace CafeMaestro.Models
{
    public class Bean
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

        public Bean()
        {
            // Set remaining quantity to match initial quantity by default
            RemainingQuantity = Quantity;
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