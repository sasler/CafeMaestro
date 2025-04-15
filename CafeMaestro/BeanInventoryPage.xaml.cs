using System.Collections.ObjectModel;
using System.Windows.Input;
using CafeMaestro.Models;
using CafeMaestro.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CafeMaestro;

[QueryProperty(nameof(NavParams), "NavParams")]
public partial class BeanInventoryPage : ContentPage
{
    private NavigationParameters? _navParams; // Make nullable to fix constructor warning
    public NavigationParameters NavParams
    {
        get => _navParams ?? new NavigationParameters();
        set
        {
            _navParams = value;
            // When navigation parameters are set, update the beans list
            if (_navParams?.AppData != null)
            {
                UpdateBeansFromAppData(_navParams.AppData);
            }
        }
    }

    private readonly BeanService _beanService;
    private readonly AppDataService _appDataService;
    private ObservableCollection<Bean> _beans;
    public ICommand RefreshCommand { get; private set; }
    public ICommand EditBeanCommand { get; private set; }
    public ICommand DeleteBeanCommand { get; private set; }

    public BeanInventoryPage(BeanService? beanService = null, AppDataService? appDataService = null)
    {
        InitializeComponent();

        // First try to get the services from the application resources (our stored service provider)
        object? serviceProviderObj = null;
        if (Application.Current?.Resources != null && 
            Application.Current.Resources.TryGetValue("ServiceProvider", out serviceProviderObj) && 
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

        // IMPORTANT: Ensure we're using the latest path from preferences
        if (Application.Current is App app)
        {
            // Get the data from the app directly instead of creating new services
            var appData = app.GetAppData();
            System.Diagnostics.Debug.WriteLine($"BeanInventoryPage constructor - Getting data directly from App: {_appDataService.DataFilePath}");
            
            // Force the AppDataService to use the same path as the main app
            Task.Run(async () => {
                try {
                    // Get the path from preferences to ensure it's the user-defined one
                    var preferencesService = serviceProviderObj is IServiceProvider sp ? 
                        sp.GetService<PreferencesService>() : 
                        Application.Current?.Handler?.MauiContext?.Services.GetService<PreferencesService>();
                        
                    if (preferencesService != null) {
                        string? savedPath = await preferencesService.GetAppDataFilePathAsync();
                        if (!string.IsNullOrEmpty(savedPath)) {
                            System.Diagnostics.Debug.WriteLine($"BeanInventoryPage - Setting path from preferences: {savedPath}");
                            await _appDataService.SetCustomFilePathAsync(savedPath);
                        }
                    }
                } catch (Exception ex) {
                    System.Diagnostics.Debug.WriteLine($"Error setting file path in BeanInventoryPage: {ex.Message}");
                }
            });
        }

        System.Diagnostics.Debug.WriteLine($"BeanInventoryPage constructor - Using AppDataService at path: {_appDataService.DataFilePath}");

        _beans = new ObservableCollection<Bean>();
        BeansCollection.ItemsSource = _beans;

        // Setup commands
        RefreshCommand = new Command(async () => await LoadBeans());
        EditBeanCommand = new Command<Bean>(async (bean) => await EditBean(bean));
        DeleteBeanCommand = new Command<Bean>(async (bean) => await DeleteBean(bean));

        // Don't set BindingContext to this since we'll use NavigationParameters
        
        // Subscribe to data changes
        _appDataService.DataChanged += OnAppDataChanged;
        
        // Subscribe to navigation events to refresh data when returning to this page
        this.Loaded += BeanInventoryPage_Loaded;
        this.NavigatedTo += BeanInventoryPage_NavigatedTo;
    }
    
    private void BeanInventoryPage_Loaded(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("BeanInventoryPage_Loaded event triggered");
        // Refresh data when page is loaded
        MainThread.BeginInvokeOnMainThread(async () => 
        {
            await ForceRefreshData();
        });
    }
    
    private void BeanInventoryPage_NavigatedTo(object? sender, NavigatedToEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("BeanInventoryPage_NavigatedTo event triggered");
        // Refresh data when navigated to this page
        MainThread.BeginInvokeOnMainThread(async () => 
        {
            await ForceRefreshData();
        });
    }
    
    // Force a full refresh from the data store
    private async Task ForceRefreshData()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Force refreshing bean inventory data");
            
            // Force a reload from the data file
            await _appDataService.ReloadDataAsync();
            
            // Get the fresh beans
            var beans = await _beanService.GetAllBeansAsync();
            
            // Update the UI
            MainThread.BeginInvokeOnMainThread(() => 
            {
                _beans.Clear();
                
                // Sort by purchase date (newest first)
                foreach (var bean in beans.OrderByDescending(b => b.PurchaseDate))
                {
                    _beans.Add(bean);
                }
                
                System.Diagnostics.Debug.WriteLine($"Force refreshed bean inventory with {_beans.Count} beans");
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error force refreshing bean inventory: {ex.Message}");
        }
    }
    
    private void OnAppDataChanged(object? sender, AppData appData)
    {
        // Reload the UI with the new data
        MainThread.BeginInvokeOnMainThread(() => {
            UpdateBeansFromAppData(appData);
        });
    }
    
    private void UpdateBeansFromAppData(AppData appData)
    {
        _beans.Clear();
        
        // Sort by purchase date (newest first)
        foreach (var bean in appData.Beans.OrderByDescending(b => b.PurchaseDate))
        {
            _beans.Add(bean);
        }
        
        System.Diagnostics.Debug.WriteLine($"Updated BeanInventoryPage with {_beans.Count} beans from AppData");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        System.Diagnostics.Debug.WriteLine("BeanInventoryPage.OnAppearing - Refreshing beans list");
        
        // Always refresh data from the service to ensure we're showing the latest
        await LoadBeans();
        
        // Get the data from our binding context if available - can be used as a fallback
        if (BindingContext is NavigationParameters navParams && navParams.AppData != null)
        {
            UpdateBeansFromAppData(navParams.AppData);
        }
    }

    private async Task LoadBeans()
    {
        try
        {
            BeansRefreshView.IsRefreshing = true;

            // Use the current data from AppDataService directly
            var appData = _appDataService.CurrentData;
            UpdateBeansFromAppData(appData);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load beans: {ex.Message}", "OK");
        }
        finally
        {
            BeansRefreshView.IsRefreshing = false;
        }
    }

    private async void OnBeanSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Bean selectedBean)
        {
            // Clear selection
            BeansCollection.SelectedItem = null;

            // Show action sheet for this bean
            string action = await DisplayActionSheet(
                selectedBean.DisplayName,
                "Cancel",
                null,
                "Edit Bean",
                "Delete Bean");

            switch (action)
            {
                case "Edit Bean":
                    await EditBean(selectedBean);
                    break;
                case "Delete Bean":
                    await DeleteBean(selectedBean);
                    break;
            }
        }
    }

    private async void AddBean_Clicked(object sender, EventArgs e)
    {
        // Create BeanEditPage directly but pass in our services
        var beanEditPage = new BeanEditPage(new Bean(), _beanService, _appDataService);
        await Navigation.PushAsync(beanEditPage);
    }

    private async Task EditBean(Bean bean)
    {
        try {
            // First fetch the fresh bean from the service to ensure we have the latest data
            // and that we're working with the exact instance that's in the collection
            var freshBean = await _beanService.GetBeanByIdAsync(bean.Id);
            
            if (freshBean == null)
            {
                System.Diagnostics.Debug.WriteLine($"Bean with ID {bean.Id} not found when trying to edit");
                await DisplayAlert("Error", "Bean not found. Please refresh and try again.", "OK");
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"Editing bean with ID: {freshBean.Id}, Name: {freshBean.CoffeeName}");
            
            // Create BeanEditPage with the fresh bean and pass in our services
            var beanEditPage = new BeanEditPage(freshBean, _beanService, _appDataService);
            await Navigation.PushAsync(beanEditPage);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error preparing to edit bean: {ex.Message}");
            await DisplayAlert("Error", "Error preparing to edit bean", "OK");
        }
    }

    private async Task DeleteBean(Bean bean)
    {
        bool confirm = await DisplayAlert(
            "Delete Bean",
            $"Are you sure you want to delete {bean.DisplayName}?",
            "Delete",
            "Cancel");

        if (confirm)
        {
            bool success = await _beanService.DeleteBeanAsync(bean.Id);

            if (success)
            {
                // Refresh the list
                await LoadBeans();
            }
            else
            {
                await DisplayAlert("Error", "Failed to delete bean", "OK");
            }
        }
    }

    private async void BackButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            // Use Shell.Current.GoToAsync to navigate back to the MainPage
            await Shell.Current.GoToAsync("//MainPage");
            System.Diagnostics.Debug.WriteLine("Navigating back to MainPage using absolute route");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error navigating back: {ex.Message}");
            // Fallback to direct Shell navigation if GoToAsync fails
            if (Shell.Current?.Items.Count > 0)
            {
                Shell.Current.CurrentItem = Shell.Current.Items[0]; // MainPage is the first item
                System.Diagnostics.Debug.WriteLine("Navigated back using direct Shell.Current.CurrentItem assignment");
            }
        }
    }
}