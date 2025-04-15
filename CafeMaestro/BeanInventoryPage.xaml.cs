using System.Collections.ObjectModel;
using System.Windows.Input;
using CafeMaestro.Models;
using CafeMaestro.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CafeMaestro;

public partial class BeanInventoryPage : ContentPage
{
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
        if (Application.Current?.Resources?.TryGetValue("ServiceProvider", out var serviceProviderObj) == true && 
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

        System.Diagnostics.Debug.WriteLine($"BeanInventoryPage constructor - Using AppDataService at path: {_appDataService.DataFilePath}");

        _beans = new ObservableCollection<Bean>();
        BeansCollection.ItemsSource = _beans;

        // Setup commands
        RefreshCommand = new Command(async () => await LoadBeans());
        EditBeanCommand = new Command<Bean>(async (bean) => await EditBean(bean));
        DeleteBeanCommand = new Command<Bean>(async (bean) => await DeleteBean(bean));

        BindingContext = this;

        // Load data initially
        InitializePageAsync();
    }

    private async void InitializePageAsync()
    {
        try
        {
            // Force reload from the AppDataService to ensure we have the correct path
            await _appDataService.ReloadDataAsync();
            
            // Then load beans
            await LoadBeans();
            
            System.Diagnostics.Debug.WriteLine($"BeanInventoryPage initialized with path: {_appDataService.DataFilePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing BeanInventoryPage: {ex.Message}");
            await DisplayAlert("Error", "Failed to initialize bean inventory data.", "OK");
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // First ensure the service is fully initialized
        try 
        {
            // Force reload data from the AppDataService to ensure we have the latest
            await _appDataService.ReloadDataAsync();
            
            // Then load the beans
            await LoadBeans();
            
            System.Diagnostics.Debug.WriteLine($"BeanInventoryPage loaded beans successfully on appearance from: {_appDataService.DataFilePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in BeanInventoryPage.OnAppearing: {ex.Message}");
            await DisplayAlert("Error", "Failed to load bean data. Please try again.", "OK");
        }
    }

    private async Task LoadBeans()
    {
        try
        {
            BeansRefreshView.IsRefreshing = true;

            List<Bean> beans = await _beanService.GetAllBeansAsync();

            _beans.Clear();

            // Sort by purchase date (newest first)
            foreach (var bean in beans.OrderByDescending(b => b.PurchaseDate))
            {
                _beans.Add(bean);
            }
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
        // Create BeanEditPage directly but pass in our services
        var beanEditPage = new BeanEditPage(bean, _beanService, _appDataService);
        await Navigation.PushAsync(beanEditPage);
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
        await Navigation.PopAsync();
    }
}