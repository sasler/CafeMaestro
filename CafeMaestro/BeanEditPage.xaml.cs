using CafeMaestro.Models;
using CafeMaestro.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CafeMaestro;

public partial class BeanEditPage : ContentPage
{
    private readonly BeanService _beanService;
    private readonly AppDataService _appDataService;
    private readonly Bean _bean;
    private readonly bool _isNewBean;

    public BeanEditPage(Bean bean, BeanService? beanService = null, AppDataService? appDataService = null)
    {
        InitializeComponent();

        // First try to get the services from the application resources (our stored service provider)
        if (Application.Current?.Resources != null && 
            Application.Current?.Resources.TryGetValue("ServiceProvider", out var serviceProviderObj) == true && 
            serviceProviderObj is IServiceProvider serviceProvider)
        {
            _appDataService = appDataService ?? 
                             serviceProvider.GetService<AppDataService>() ??
                             Application.Current?.Handler?.MauiContext?.Services.GetService<AppDataService>() ??
                             throw new InvalidOperationException("AppDataService not available");
            
            _beanService = beanService ?? 
                          serviceProvider.GetService<BeanService>() ??
                          Application.Current?.Handler?.MauiContext?.Services.GetService<BeanService>() ??
                          throw new InvalidOperationException("BeanService not available");
        }
        else
        {
            // Fall back to the old way if app resources doesn't have our provider
            _appDataService = appDataService ?? 
                            Application.Current?.Handler?.MauiContext?.Services.GetService<AppDataService>() ??
                            throw new InvalidOperationException("AppDataService not available");
            
            _beanService = beanService ?? 
                          Application.Current?.Handler?.MauiContext?.Services.GetService<BeanService>() ??
                          throw new InvalidOperationException("BeanService not available");
        }

        System.Diagnostics.Debug.WriteLine($"BeanEditPage constructor - Using AppDataService at path: {_appDataService.DataFilePath}");

        _bean = bean;
        BindingContext = _bean;

        // Determine if this is a new bean (has empty GUID) or an existing one
        _isNewBean = _bean.Id == Guid.Empty;
        
        // Set page title based on whether this is a new bean or editing existing
        Title = _isNewBean ? "Add Bean" : "Edit Bean";
        PageTitleLabel.Text = _isNewBean ? "Add New Bean" : "Edit Bean";

        // Set default values for new bean
        if (_isNewBean)
        {
            // Generate a new ID for new beans
            _bean.Id = Guid.NewGuid();
            System.Diagnostics.Debug.WriteLine($"Generated new bean ID: {_bean.Id}");
            _bean.PurchaseDate = DateTime.Now;
            _bean.RemainingQuantity = _bean.Quantity; // Default to full quantity
        }
        else
        {
            // Log the ID of the existing bean
            System.Diagnostics.Debug.WriteLine($"Editing existing bean with ID: {_bean.Id}");
        }

        // Format the purchase date
        PurchaseDatePicker.Date = _bean.PurchaseDate;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            // Log bean details for debugging
            System.Diagnostics.Debug.WriteLine($"BeanEditPage.OnAppearing - Bean ID: {_bean.Id}, Name: {_bean.CoffeeName}, Price: {_bean.Price?.ToString() ?? "null"}");
            
            // Ensure all UI fields are properly populated with the bean values
            CoffeeNameEntry.Text = _bean.CoffeeName;
            CountryEntry.Text = _bean.Country;
            VarietyEntry.Text = _bean.Variety;
            
            // Set process picker
            if (!string.IsNullOrEmpty(_bean.Process))
            {
                for (int i = 0; i < ProcessPicker.Items.Count; i++)
                {
                    if (ProcessPicker.Items[i].Equals(_bean.Process, StringComparison.OrdinalIgnoreCase))
                    {
                        ProcessPicker.SelectedIndex = i;
                        break;
                    }
                }
            }
            
            PurchaseDatePicker.Date = _bean.PurchaseDate;
            QuantityEntry.Text = _bean.Quantity.ToString("F2");
            PriceEntry.Text = _bean.Price?.ToString("F2") ?? string.Empty;
            LinkEntry.Text = _bean.Link;
            NotesEditor.Text = _bean.Notes;

            System.Diagnostics.Debug.WriteLine($"BeanEditPage form populated - Bean: {_bean.CoffeeName}, Price: {_bean.Price?.ToString() ?? "null"}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in BeanEditPage.OnAppearing: {ex.Message}");
        }
    }

    private async void SaveButton_Clicked(object sender, EventArgs e)
    {
        if (!ValidateInputs())
        {
            return;
        }

        try
        {
            // Update bean with all form values
            _bean.CoffeeName = CoffeeNameEntry.Text?.Trim() ?? "";
            _bean.Country = CountryEntry.Text?.Trim() ?? "";
            _bean.Variety = VarietyEntry.Text?.Trim() ?? "";
            _bean.Process = ProcessPicker.SelectedItem?.ToString() ?? "";
            _bean.PurchaseDate = PurchaseDatePicker.Date;
            _bean.Notes = NotesEditor.Text?.Trim() ?? "";
            _bean.Link = LinkEntry.Text?.Trim() ?? "";
            
            // ValidateInputs() already set Quantity and Price
            
            System.Diagnostics.Debug.WriteLine($"Saving bean: {_bean.CoffeeName}, ID: {_bean.Id}, Price: {_bean.Price?.ToString() ?? "null"}");
            
            bool success;
            
            // Check if this is a new bean or an update based on the flag we set in constructor
            if (_isNewBean)
            {
                // Make sure we have a valid ID
                if (_bean.Id == Guid.Empty)
                {
                    _bean.Id = Guid.NewGuid();
                    System.Diagnostics.Debug.WriteLine($"Generated new ID for bean: {_bean.Id}");
                }
                
                success = await _beanService.AddBeanAsync(_bean);
                System.Diagnostics.Debug.WriteLine($"Result of adding new bean: {success}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Updating existing bean with ID: {_bean.Id}");
                success = await _beanService.UpdateBeanAsync(_bean);
                System.Diagnostics.Debug.WriteLine($"Result of updating bean: {success}");
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
            System.Diagnostics.Debug.WriteLine($"Error saving bean: {ex.Message}");
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
        else
        {
            // Explicitly set Price to null when field is empty
            _bean.Price = null;
        }
        
        return true;
    }

    private void CancelButton_Clicked(object sender, EventArgs e)
    {
        Navigation.PopAsync();
    }
    
    // Override OnBackButtonPressed to handle Android back button
    protected override bool OnBackButtonPressed()
    {
        // Use the same logic as CancelButton_Clicked but in a synchronous way
        MainThread.BeginInvokeOnMainThread(() => {
            Navigation.PopAsync();
            System.Diagnostics.Debug.WriteLine("Navigated back using hardware back button in BeanEditPage");
        });
        return true; // Indicate we've handled the back button
    }
}