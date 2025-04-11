using CafeMaestro.Models;
using CafeMaestro.Services;

namespace CafeMaestro;

public partial class BeanEditPage : ContentPage
{
    private readonly BeanService _beanService;
    private readonly AppDataService _appDataService;
    private readonly Bean _bean;

    public BeanEditPage(Bean bean)
    {
        InitializeComponent();

        // Get services from DI
        _appDataService = Application.Current?.Handler?.MauiContext?.Services.GetService<AppDataService>() ?? 
                         new AppDataService();
        _beanService = Application.Current?.Handler?.MauiContext?.Services.GetService<BeanService>() ?? 
                      new BeanService(_appDataService);

        _bean = bean;
        BindingContext = _bean;

        // Set page title based on whether this is a new bean or editing existing
        Title = _bean.Id == Guid.Empty ? "Add Bean" : "Edit Bean";

        // Set default values for new bean
        if (_bean.Id == Guid.Empty)
        {
            _bean.Id = Guid.NewGuid();
            _bean.PurchaseDate = DateTime.Now;
            _bean.RemainingQuantity = _bean.Quantity; // Default to full quantity
        }

        // Format the purchase date
        PurchaseDatePicker.Date = _bean.PurchaseDate;
    }

    private async void SaveButton_Clicked(object sender, EventArgs e)
    {
        if (!ValidateInputs())
        {
            return;
        }

        try
        {
            // Update bean with form values
            _bean.PurchaseDate = PurchaseDatePicker.Date;
            
            bool success;
            
            // Check if this is a new bean or an update
            if (_bean.Id == Guid.Empty)
            {
                _bean.Id = Guid.NewGuid();
                success = await _beanService.AddBeanAsync(_bean);
            }
            else
            {
                success = await _beanService.UpdateBeanAsync(_bean);
            }
            
            if (success)
            {
                await DisplayAlert("Success", "Bean saved successfully", "OK");
                await Navigation.PopAsync();
            }
            else
            {
                await DisplayAlert("Error", "Failed to save bean", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
        }
    }

    private bool ValidateInputs()
    {
        // Validate coffee name
        if (string.IsNullOrWhiteSpace(CoffeeNameEntry.Text))
        {
            DisplayAlert("Validation Error", "Please enter a coffee name", "OK");
            return false;
        }
        
        // Validate country
        if (string.IsNullOrWhiteSpace(CountryEntry.Text))
        {
            DisplayAlert("Validation Error", "Please enter a country of origin", "OK");
            return false;
        }
        
        // Validate quantity
        if (string.IsNullOrWhiteSpace(QuantityEntry.Text) || 
            !double.TryParse(QuantityEntry.Text, out double quantity) || 
            quantity <= 0)
        {
            DisplayAlert("Validation Error", "Please enter a valid quantity", "OK");
            return false;
        }
        
        // Update remaining quantity if this is a new bean
        if (_bean.Id == Guid.Empty || _bean.RemainingQuantity == 0)
        {
            _bean.RemainingQuantity = quantity;
        }
        else if (quantity < _bean.Quantity)
        {
            // If reducing total quantity, reduce remaining proportionally
            double ratio = quantity / _bean.Quantity;
            _bean.RemainingQuantity = Math.Min(_bean.RemainingQuantity, quantity) * ratio;
        }
        else if (quantity > _bean.Quantity)
        {
            // If increasing total quantity, increase remaining by the difference
            double increase = quantity - _bean.Quantity;
            _bean.RemainingQuantity += increase;
        }
        
        // Set the parsed quantity
        _bean.Quantity = quantity;
        
        // Validate price (optional)
        if (!string.IsNullOrWhiteSpace(PriceEntry.Text))
        {
            if (!decimal.TryParse(PriceEntry.Text, out decimal price) || price < 0)
            {
                DisplayAlert("Validation Error", "Please enter a valid price", "OK");
                return false;
            }
            
            _bean.Price = price;
        }
        
        return true;
    }

    private void CancelButton_Clicked(object sender, EventArgs e)
    {
        Navigation.PopAsync();
    }
}