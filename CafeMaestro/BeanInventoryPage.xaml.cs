using System.Collections.ObjectModel;
using System.Windows.Input;
using CafeMaestro.Models;
using CafeMaestro.Services;

namespace CafeMaestro;

public partial class BeanInventoryPage : ContentPage
{
    private readonly BeanService _beanService;
    private readonly AppDataService _appDataService;
    private ObservableCollection<Bean> _beans;
    public ICommand RefreshCommand { get; private set; }
    public ICommand EditBeanCommand { get; private set; }
    public ICommand DeleteBeanCommand { get; private set; }

    public BeanInventoryPage()
    {
        InitializeComponent();

        // Get services from DI
        _appDataService = Application.Current?.Handler?.MauiContext?.Services.GetService<AppDataService>() ?? 
                         new AppDataService();
        _beanService = Application.Current?.Handler?.MauiContext?.Services.GetService<BeanService>() ?? 
                      new BeanService(_appDataService);

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
        await LoadBeans();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadBeans();
    }

    private async Task LoadBeans()
    {
        try
        {
            BeansRefreshView.IsRefreshing = true;

            List<Bean> beans = await _beanService.LoadBeansAsync();

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
        await Navigation.PushAsync(new BeanEditPage(new Bean()));
    }

    private async Task EditBean(Bean bean)
    {
        await Navigation.PushAsync(new BeanEditPage(bean));
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