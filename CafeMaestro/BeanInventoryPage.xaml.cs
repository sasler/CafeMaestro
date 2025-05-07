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

    private readonly BeanDataService _beanService;
    private readonly AppDataService _appDataService;
    private ObservableCollection<BeanData> _beans;
    public ICommand RefreshCommand { get; private set; }
    public ICommand EditBeanCommand { get; private set; }
    public ICommand DeleteBeanCommand { get; private set; }
    public ICommand ItemTappedCommand { get; private set; }

    public BeanInventoryPage(BeanDataService? beanService = null, AppDataService? appDataService = null)
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

        // IMPORTANT: Ensure we're using the latest path from preferences
        if (Application.Current is App app)
        {
            // Get the data from the app directly instead of creating new services
            var appData = app.GetAppData();

            // Force the AppDataService to use the same path as the main app
            Task.Run(async () =>
            {
                try
                {
                    // Get the path from preferences to ensure it's the user-defined one
                    var preferencesService = serviceProviderObj is IServiceProvider sp ?
                        sp.GetService<PreferencesService>() :
                        Application.Current?.Handler?.MauiContext?.Services.GetService<PreferencesService>();

                    if (preferencesService != null)
                    {
                        string? savedPath = await preferencesService.GetAppDataFilePathAsync();
                        if (!string.IsNullOrEmpty(savedPath))
                        {
                            await _appDataService.SetCustomFilePathAsync(savedPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error setting file path in BeanInventoryPage: {ex.Message}");
                }
            });
        }

        _beans = new ObservableCollection<BeanData>();
        BeansCollection.ItemsSource = _beans;

        // Setup commands for SwipeView actions
        RefreshCommand = new Command(async () => await LoadBeans());

        // Edit command - directly navigate to edit page for the bean
        EditBeanCommand = new Command<BeanData>(async (bean) =>
        {
            try
            {
                // First fetch the fresh bean from the service to ensure we have the latest data
                var freshBean = await _beanService.GetBeanByIdAsync(bean.Id);

                if (freshBean == null)
                {
                    await DisplayAlert("Error", "Bean not found. Please refresh and try again.", "OK");
                    return;
                }

                // Create BeanEditPage with the fresh bean and pass in our services
                var beanEditPage = new BeanEditPage(freshBean, _beanService, _appDataService);
                await Navigation.PushAsync(beanEditPage);
            }
            catch
            {
                await DisplayAlert("Error", "Error preparing to edit bean", "OK");
            }
        });

        // Delete command - directly delete without showing a confirmation dialog
        DeleteBeanCommand = new Command<BeanData>(async (bean) =>
        {
            bool success = await _beanService.DeleteBeanAsync(bean.Id);

            if (success)
            {
                // Refresh the list after successful deletion
                await LoadBeans();
            }
            else
            {
                await DisplayAlert("Error", "Failed to delete bean", "OK");
            }
        });

        // Item tapped command - for Windows mouse users since SwipeView only works with touch
        ItemTappedCommand = new Command<BeanData>(async (bean) =>
        {
            try
            {
                // Show action sheet with options
                string action = await DisplayActionSheet(
                    bean.DisplayName,
                    "Cancel",
                    null,
                    "Edit",
                    "Delete");

                switch (action)
                {
                    case "Edit":
                        await EditBean(bean);
                        break;
                    case "Delete":
                        await DeleteBean(bean);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling item tap: {ex.Message}");
            }
        });

        // Subscribe to data changes
        _appDataService.DataChanged += OnAppDataChanged;

        // Subscribe to navigation events to refresh data when returning to this page
        this.Loaded += BeanInventoryPage_Loaded;
        this.NavigatedTo += BeanInventoryPage_NavigatedTo;
    }

    private void BeanInventoryPage_Loaded(object? sender, EventArgs e)
    {
        // Refresh data when page is loaded
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await ForceRefreshData();
        });
    }

    private void BeanInventoryPage_NavigatedTo(object? sender, NavigatedToEventArgs e)
    {
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
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error force refreshing bean inventory: {ex.Message}");
        }
    }

    private void UpdateRecordCount(int count)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            RecordCountLabel.Text = $"{count}";
        });
    }

    private void OnAppDataChanged(object? sender, AppData appData)
    {
        // Reload the UI with the new data
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateBeansFromAppData(appData);
            // Update the record count display
            UpdateRecordCount(appData.Beans?.Count ?? 0);
        });
    }

    private void UpdateBeansFromAppData(AppData appData)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                // Clear the collection on the UI thread
                _beans.Clear();

                // Track how many beans we're adding for debugging
                int count = 0;

                // Sort by purchase date (newest first) and add each bean
                foreach (var bean in appData.Beans.OrderByDescending(b => b.PurchaseDate))
                {
                    // Make sure we're adding a valid bean with required properties
                    if (bean != null && bean.Id != Guid.Empty)
                    {
                        _beans.Add(bean);
                        count++;
                    }
                }

                // Update the record count display
                UpdateRecordCount(count);

                // Force refresh the CollectionView
                BeansCollection.ItemsSource = null;
                BeansCollection.ItemsSource = _beans;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in UpdateBeansFromAppData: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Set RefreshView's command execution
        BeansRefreshView.Command = new Command(async () =>
        {
            try
            {
                await RefreshBeans();
            }
            finally
            {
                // Always ensure IsRefreshing is set to false when complete
                BeansRefreshView.IsRefreshing = false;
            }
        });

        // Initial load of beans - awaited with _ to suppress warning while still allowing execution to continue
        _ = RefreshBeans();
    }

    private async Task RefreshBeans()
    {
        try
        {
            // Get all beans from service
            var beans = await _beanService.GetAllBeansAsync();

            // Update UI on main thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                // Clear existing beans
                _beans.Clear();

                // Add beans in order (newest first)
                foreach (var bean in beans.OrderByDescending(b => b.PurchaseDate))
                {
                    _beans.Add(bean);
                }

                // Update the record count
                UpdateRecordCount(beans.Count);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error refreshing beans: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private async void OnBeanSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is BeanData selectedBean)
        {
            // Clear selection
            BeansCollection.SelectedItem = null;

            // Show action sheet for this bean
            string action = await DisplayActionSheet(
                selectedBean.DisplayName,
                "Cancel",
                null,
                "Edit",
                "Delete");

            switch (action)
            {
                case "Edit":
                    await EditBean(selectedBean);
                    break;
                case "Delete":
                    await DeleteBean(selectedBean);
                    break;
            }
        }
    }

    private async void AddBean_Clicked(object sender, EventArgs e)
    {
        // Create BeanEditPage directly but pass in our services
        var beanEditPage = new BeanEditPage(new BeanData(), _beanService, _appDataService);
        await Navigation.PushAsync(beanEditPage);
    }

    private async Task EditBean(BeanData bean)
    {
        try
        {
            // First fetch the fresh bean from the service to ensure we have the latest data
            // and that we're working with the exact instance that's in the collection
            var freshBean = await _beanService.GetBeanByIdAsync(bean.Id);

            if (freshBean == null)
            {
                await DisplayAlert("Error", "Bean not found. Please refresh and try again.", "OK");
                return;
            }

            // Create BeanEditPage with the fresh bean and pass in our services
            var beanEditPage = new BeanEditPage(freshBean, _beanService, _appDataService);
            await Navigation.PushAsync(beanEditPage);
        }
        catch
        {
            await DisplayAlert("Error", "Error preparing to edit bean", "OK");
        }
    }

    private async Task DeleteBean(BeanData bean)
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
        }
        catch
        {
            // Fallback to direct Shell navigation if GoToAsync fails
            if (Shell.Current?.Items.Count > 0)
            {
                Shell.Current.CurrentItem = Shell.Current.Items[0]; // MainPage is the first item
            }
        }
    }

    private async void ImportButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            // Create BeanImportPage and pass in our services
            var beanImportPage = new BeanImportPage(_beanService, _appDataService);
            await Navigation.PushAsync(beanImportPage);
        }
        catch
        {
            await DisplayAlert("Error", "Unable to open import page", "OK");
        }
    }

    private async void DeleteBean_Clicked(object sender, EventArgs e)
    {
        if (sender is BindableObject bindable && bindable.BindingContext is BeanData bean)
        {
            await DeleteBean(bean);
        }
    }

    private async Task LoadBeans()
    {
        try
        {
            BeansRefreshView.IsRefreshing = true;

            // Get all beans from the service
            var beans = await _beanService.GetAllBeansAsync();

            // Update the UI on the main thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _beans.Clear();

                foreach (var bean in beans.OrderByDescending(b => b.PurchaseDate))
                {
                    _beans.Add(bean);
                }
            });
        }
        catch
        {
            await DisplayAlert("Error", "Failed to load beans", "OK");
        }
        finally
        {
            BeansRefreshView.IsRefreshing = false;
        }
    }

    // Override OnBackButtonPressed to handle Android back button
    protected override bool OnBackButtonPressed()
    {
        // Use the same logic as BackButton_Clicked but in a synchronous way
        try
        {
            // Navigate back to MainPage using direct Shell.CurrentItem assignment
            // This works better on Android than GoToAsync
            if (Shell.Current?.Items.Count > 0)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Shell.Current.CurrentItem = Shell.Current.Items[0]; // MainPage is the first item
                });
                return true; // Indicate we've handled the back button
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling back button: {ex.Message}");
        }

        return base.OnBackButtonPressed(); // Let the system handle it if our code fails
    }
}