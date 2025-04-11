using CafeMaestro.Models;
using CafeMaestro.Services;

namespace CafeMaestro;

public partial class BeanEditPage : ContentPage
{
    private readonly BeanService _beanService;
    private Bean _bean;
    private bool _isEditMode;

    public BeanEditPage(Bean? beanToEdit = null)
    {
        InitializeComponent();

        // Get service from DI
        _beanService = Application.Current?.Handler?.MauiContext?.Services.GetService<BeanService>() ?? 
                       new BeanService();
        
        _isEditMode = beanToEdit != null;
        
        if (_isEditMode)
        {
            _bean = beanToEdit ?? new Bean();
            PageTitleLabel.Text = "Edit Bean";
            LoadBeanData();
        }
        else
        {
            _bean = new Bean();
            
            // Set default values
            PurchaseDatePicker.Date = DateTime.Now;
            if (ProcessPicker.Items.Count > 0)
                ProcessPicker.SelectedIndex = 0;
        }
    }

    private void LoadBeanData()
    {
        CoffeeNameEntry.Text = _bean.CoffeeName;
        CountryEntry.Text = _bean.Country;
        VarietyEntry.Text = _bean.Variety;
        
        // Find the process in the picker items
        int processIndex = ProcessPicker.Items.IndexOf(_bean.Process);
        if (processIndex >= 0)
            ProcessPicker.SelectedIndex = processIndex;
        else if (ProcessPicker.Items.Count > 0)
            ProcessPicker.SelectedIndex = ProcessPicker.Items.Count - 1; // "Other"
        
        PurchaseDatePicker.Date = _bean.PurchaseDate;
        QuantityEntry.Text = _bean.Quantity.ToString("F2");
        PriceEntry.Text = _bean.Price.ToString("F2");
        LinkEntry.Text = _bean.Link;
        NotesEditor.Text = _bean.Notes;
    }

    private bool ValidateInputs()
    {
        // Validate coffee name
        if (string.IsNullOrWhiteSpace(CoffeeNameEntry.Text))
        {
            DisplayAlert("Validation Error", "Please enter a coffee name.", "OK");
            return false;
        }

        // Validate country
        if (string.IsNullOrWhiteSpace(CountryEntry.Text))
        {
            DisplayAlert("Validation Error", "Please enter a country of origin.", "OK");
            return false;
        }

        // Validate process
        if (ProcessPicker.SelectedItem == null)
        {
            DisplayAlert("Validation Error", "Please select a processing method.", "OK");
            return false;
        }

        // Validate quantity
        if (string.IsNullOrWhiteSpace(QuantityEntry.Text) ||
            !double.TryParse(QuantityEntry.Text, out double quantity) ||
            quantity <= 0)
        {
            DisplayAlert("Validation Error", "Please enter a valid quantity in kg.", "OK");
            return false;
        }

        // Validate price
        if (string.IsNullOrWhiteSpace(PriceEntry.Text) ||
            !decimal.TryParse(PriceEntry.Text, out decimal price) ||
            price < 0)
        {
            DisplayAlert("Validation Error", "Please enter a valid price.", "OK");
            return false;
        }

        return true;
    }

    private async void SaveBean_Clicked(object sender, EventArgs e)
    {
        if (!ValidateInputs())
            return;

        try
        {
            // Update bean object with form values
            _bean.CoffeeName = CoffeeNameEntry.Text;
            _bean.Country = CountryEntry.Text;
            _bean.Variety = VarietyEntry.Text;
            _bean.Process = ProcessPicker.SelectedItem?.ToString() ?? "Unknown";
            _bean.PurchaseDate = PurchaseDatePicker.Date;
            _bean.Notes = NotesEditor.Text;
            _bean.Link = LinkEntry.Text;
            
            double.TryParse(QuantityEntry.Text, out double quantity);
            _bean.Quantity = quantity;
            
            // Only update remaining quantity for new beans
            if (!_isEditMode)
                _bean.RemainingQuantity = quantity;
                
            decimal.TryParse(PriceEntry.Text, out decimal price);
            _bean.Price = price;

            bool success;
            if (_isEditMode)
            {
                success = await _beanService.UpdateBeanAsync(_bean);
            }
            else
            {
                success = await _beanService.AddBeanAsync(_bean);
            }

            if (success)
            {
                await Navigation.PopAsync();
            }
            else
            {
                await DisplayAlert("Error", "Failed to save bean data. Please try again.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
        }
    }

    private async void Cancel_Clicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}