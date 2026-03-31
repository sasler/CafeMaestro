using System.Globalization;
using CafeMaestro.Models;
using CafeMaestro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafeMaestro.ViewModels;

public partial class BeanEditPageViewModel : ObservableObject, IQueryAttributable
{
    private readonly IBeanDataService _beanService;
    private readonly INavigationService _navigationService;
    private Guid _beanId = Guid.Empty;
    private bool _isNewBean = true;

    [ObservableProperty]
    public partial string CoffeeName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Country { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Variety { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Process { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Notes { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Quantity { get; set; } = "0.00";

    [ObservableProperty]
    public partial string Price { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Link { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DateTime PurchaseDate { get; set; } = DateTime.Now;

    [ObservableProperty]
    public partial string RemainingQuantity { get; set; } = "0.00";

    [ObservableProperty]
    public partial string PageTitle { get; set; } = "Add Bean";

    [ObservableProperty]
    public partial string PageHeading { get; set; } = "Add New Bean";

    public BeanEditPageViewModel(IBeanDataService beanService, INavigationService navigationService)
    {
        _beanService = beanService ?? throw new ArgumentNullException(nameof(beanService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
    }

    public Func<string, string, string, Task>? AlertAsync { get; set; }

    public Task InitializationTask { get; private set; } = Task.CompletedTask;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        InitializationTask = ApplyQueryAttributesAsync(query);
    }

    public Task OnAppearingAsync()
    {
        return InitializationTask;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!TryBuildBean(out BeanData bean, out string validationMessage))
        {
            await ShowAlertAsync("Validation Error", validationMessage, "OK");
            return;
        }

        try
        {
            bool success = _isNewBean
                ? await _beanService.AddBeanAsync(bean)
                : await _beanService.UpdateBeanAsync(bean);

            if (success)
            {
                await ShowAlertAsync("Success", "Bean saved successfully", "OK");
                await _navigationService.GoBackAsync();
            }
            else
            {
                await ShowAlertAsync("Error", "Failed to save bean", "OK");
            }
        }
        catch (Exception ex)
        {
            await ShowAlertAsync("Error", $"An error occurred: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private Task CancelAsync()
    {
        return _navigationService.GoBackAsync();
    }

    private async Task ApplyQueryAttributesAsync(IDictionary<string, object> query)
    {
        if (query.TryGetValue("IsNewBean", out object? isNewValue) &&
            TryConvertToBoolean(isNewValue))
        {
            InitializeNewBean();
            return;
        }

        if (query.TryGetValue("BeanId", out object? beanIdValue) &&
            Guid.TryParse(beanIdValue?.ToString(), out Guid beanId))
        {
            await LoadBeanAsync(beanId);
            return;
        }

        InitializeNewBean();
    }

    private async Task LoadBeanAsync(Guid beanId)
    {
        var bean = await _beanService.GetBeanByIdAsync(beanId);

        if (bean is null)
        {
            InitializeNewBean();
            await ShowAlertAsync("Error", "Bean not found. Please refresh and try again.", "OK");
            return;
        }

        _beanId = bean.Id;
        _isNewBean = false;

        PageTitle = "Edit Bean";
        PageHeading = "Edit Bean";
        CoffeeName = bean.CoffeeName;
        Country = bean.Country;
        Variety = bean.Variety;
        Process = bean.Process;
        Notes = bean.Notes;
        Quantity = bean.Quantity.ToString("F2", CultureInfo.InvariantCulture);
        RemainingQuantity = bean.RemainingQuantity.ToString("F2", CultureInfo.InvariantCulture);
        Price = bean.Price?.ToString("F2", CultureInfo.InvariantCulture) ?? string.Empty;
        Link = bean.Link;
        PurchaseDate = bean.PurchaseDate;
    }

    private void InitializeNewBean()
    {
        _beanId = Guid.NewGuid();
        _isNewBean = true;

        PageTitle = "Add Bean";
        PageHeading = "Add New Bean";
        CoffeeName = string.Empty;
        Country = string.Empty;
        Variety = string.Empty;
        Process = string.Empty;
        Notes = string.Empty;
        Quantity = "0.00";
        RemainingQuantity = "0.00";
        Price = string.Empty;
        Link = string.Empty;
        PurchaseDate = DateTime.Now;
    }

    private bool TryBuildBean(out BeanData bean, out string validationMessage)
    {
        if (string.IsNullOrWhiteSpace(CoffeeName))
        {
            bean = new BeanData();
            validationMessage = "Please enter a coffee name";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Country))
        {
            bean = new BeanData();
            validationMessage = "Please enter a country of origin";
            return false;
        }

        if (!double.TryParse(Quantity, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double quantity) || quantity <= 0)
        {
            bean = new BeanData();
            validationMessage = "Please enter a valid quantity";
            return false;
        }

        if (!double.TryParse(RemainingQuantity, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double remainingQuantity) || remainingQuantity < 0)
        {
            bean = new BeanData();
            validationMessage = "Please enter a valid remaining quantity";
            return false;
        }

        if (remainingQuantity > quantity)
        {
            bean = new BeanData();
            validationMessage = "Remaining quantity cannot exceed total quantity";
            return false;
        }

        decimal? parsedPrice = null;
        if (!string.IsNullOrWhiteSpace(Price))
        {
            if (!decimal.TryParse(Price, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal price) || price < 0)
            {
                bean = new BeanData();
                validationMessage = "Please enter a valid price";
                return false;
            }

            parsedPrice = price;
        }

        bean = new BeanData
        {
            Id = _beanId == Guid.Empty ? Guid.NewGuid() : _beanId,
            CoffeeName = CoffeeName.Trim(),
            Country = Country.Trim(),
            Variety = Variety.Trim(),
            Process = Process?.Trim() ?? string.Empty,
            PurchaseDate = PurchaseDate,
            Quantity = quantity,
            RemainingQuantity = remainingQuantity,
            Price = parsedPrice,
            Link = Link?.Trim() ?? string.Empty,
            Notes = Notes?.Trim() ?? string.Empty
        };

        validationMessage = string.Empty;
        return true;
    }

    private static bool TryConvertToBoolean(object? value)
    {
        return value switch
        {
            bool booleanValue => booleanValue,
            string stringValue when bool.TryParse(stringValue, out bool parsedValue) => parsedValue,
            _ => false
        };
    }

    private Task ShowAlertAsync(string title, string message, string cancel)
    {
        return AlertAsync?.Invoke(title, message, cancel) ?? Task.CompletedTask;
    }
}
