using System.Collections.ObjectModel;
using System.Windows.Input;
using CafeMaestro.Models;
using CafeMaestro.Services;

namespace CafeMaestro;

public partial class BeanInventoryPage : ContentPage
{
    private readonly BeanService _beanService;
    private ObservableCollection<Bean> _beans;
    public ICommand RefreshCommand { get; private set; }
    public ICommand EditBeanCommand { get; private set; }
    public ICommand DeleteBeanCommand { get; private set; }

    public BeanInventoryPage()
    {
        InitializeComponent();

        // Get service from DI
        _beanService = Application.Current?.Handler?.MauiContext?.Services.GetService<BeanService>() ?? 
                       new BeanService();

        _beans = new ObservableCollection<Bean>();
        BeansCollection.ItemsSource = _beans;

        // Setup commands
        RefreshCommand = new Command(async () => await LoadBeans());
        EditBeanCommand = new Command<Bean>(async (bean) => await EditBean(bean));
        DeleteBeanCommand = new Command<Bean>(async (bean) => await DeleteBean(bean));

        BindingContext = this;

        // Load data initially
        LoadBeans();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadBeans();
    }

    private async Task LoadBeans(string searchTerm = "")
    {
        try
        {
            BeansRefreshView.IsRefreshing = true;

            List<Bean> beans;

            if (string.IsNullOrWhiteSpace(searchTerm))
                beans = await _beanService.LoadBeansAsync();
            else
                beans = await _beanService.SearchBeansAsync(searchTerm);

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

    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        await LoadBeans(e.NewTextValue);
    }

    private async void OnBeanSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Bean selectedBean)
        {
            // Clear selection
            BeansCollection.SelectedItem = null;

            // Show details
            string details = $"Coffee: {selectedBean.CoffeeName}\n" +
                             $"Origin: {selectedBean.Country}\n" +
                             $"Variety: {selectedBean.Variety}\n" +
                             $"Process: {selectedBean.Process}\n" +
                             $"Purchase Date: {selectedBean.PurchaseDate:MM/dd/yyyy}\n" +
                             $"Quantity: {selectedBean.RemainingQuantity:F2}kg / {selectedBean.Quantity:F2}kg\n" +
                             $"Price: ${selectedBean.Price:F2}";

            if (!string.IsNullOrWhiteSpace(selectedBean.Link))
            {
                details += $"\n\nProduct Link: {selectedBean.Link}";
            }

            if (!string.IsNullOrWhiteSpace(selectedBean.Notes))
            {
                details += $"\n\nNotes:\n{selectedBean.Notes}";
            }

            await DisplayAlert($"{selectedBean.CoffeeName} Details", details, "Close");
        }
    }

    private async void AddBean_Clicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new BeanEditPage());
    }

    private async Task EditBean(Bean bean)
    {
        await Navigation.PushAsync(new BeanEditPage(bean));
    }

    private async Task DeleteBean(Bean bean)
    {
        bool confirm = await DisplayAlert("Confirm Delete", 
            $"Are you sure you want to delete {bean.CoffeeName} from your inventory?", "Yes", "No");
            
        if (!confirm)
            return;

        bool success = await _beanService.DeleteBeanAsync(bean.Id);

        if (success)
        {
            await LoadBeans();
        }
        else
        {
            await DisplayAlert("Error", "Failed to delete bean from inventory.", "OK");
        }
    }

    private async void BackButton_Clicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}