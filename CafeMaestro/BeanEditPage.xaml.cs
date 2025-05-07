using CafeMaestro.Models;
using CafeMaestro.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CafeMaestro;

public partial class BeanEditPage : ContentPage
{
    private readonly BeanDataService _beanService;
    private readonly AppDataService _appDataService;
    private readonly BeanData _bean;
    private readonly bool _isNewBean;

    public BeanEditPage(BeanData bean, BeanDataService? beanService = null, AppDataService? appDataService = null)
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
                          serviceProvider.GetService<BeanDataService>() ??
                          Application.Current?.Handler?.MauiContext?.Services.GetService<BeanDataService>() ??
                          throw new InvalidOperationException("BeanService not available");
        }
        else
        {
            // Fall back to the old way if app resources doesn't have our provider
            _appDataService = appDataService ??
                            Application.Current?.Handler?.MauiContext?.Services.GetService<AppDataService>() ??
                            throw new InvalidOperationException("AppDataService not available");

            _beanService = beanService ??
                          Application.Current?.Handler?.MauiContext?.Services.GetService<BeanDataService>() ??
                          throw new InvalidOperationException("BeanService not available");
        }

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
            _bean.PurchaseDate = DateTime.Now;
            _bean.RemainingQuantity = _bean.Quantity; // Default to full quantity
        }

        // Format the purchase date
        PurchaseDatePicker.Date = _bean.PurchaseDate;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        try
        {
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
            RemainingQuantityEntry.Text = _bean.RemainingQuantity.ToString("F2");
            PriceEntry.Text = _bean.Price?.ToString("F2") ?? string.Empty;
            LinkEntry.Text = _bean.Link;
            NotesEditor.Text = _bean.Notes;
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

            bool success;

            // Check if this is a new bean or an update based on the flag we set in constructor
            if (_isNewBean)
            {
                // Make sure we have a valid ID
                if (_bean.Id == Guid.Empty)
                {
                    _bean.Id = Guid.NewGuid();
                }

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

        // Set the parsed quantity
        // Note: The assignment of _bean.Quantity is placed before validating the remaining quantity.
        // This ordering is intentional as the validation logic uses the local variable 'quantity'
        // and does not depend on the original value of _bean.Quantity.
        _bean.Quantity = quantity;

        // Validate remaining quantity
        if (string.IsNullOrWhiteSpace(RemainingQuantityEntry.Text) ||
            !double.TryParse(RemainingQuantityEntry.Text, out double remainingQuantity) ||
            remainingQuantity < 0)
        {
            DisplayAlert("Validation Error", "Please enter a valid remaining quantity", "OK");
            return false;
        }

        // Validate remaining quantity doesn't exceed total quantity
        // Note: Unlike previous behavior, remaining quantity is now directly controlled by user input
        // rather than being automatically adjusted based on changes to the total quantity.
        if (remainingQuantity > quantity)
        {
            DisplayAlert("Validation Error", "Remaining quantity cannot exceed total quantity", "OK");
            return false;
        }

        // Set the parsed remaining quantity
        _bean.RemainingQuantity = remainingQuantity;

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
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Navigation.PopAsync();
        });
        return true; // Indicate we've handled the back button
    }
}