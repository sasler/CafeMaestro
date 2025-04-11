namespace CafeMaestro;

using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CafeMaestro.Models;
using CafeMaestro.Services;

public partial class MainPage : ContentPage
{
    private TimerService timerService;
    private RoastDataService roastDataService;
    private BeanService beanService;
    private AppDataService appDataService;
    private PreferencesService preferencesService;
    private bool isTimerUpdating = false; // Flag to prevent recursive updates
    private string temporaryDigitsBuffer = ""; // Store digits before formatting
    
    // Store the selected bean for roasting
    private Bean? selectedBean = null;
    private List<Bean> availableBeans = new List<Bean>();

    public MainPage()
    {
        InitializeComponent();

        // Initialize services - get from dependency injection
        appDataService = Application.Current?.Handler?.MauiContext?.Services.GetService<AppDataService>() ?? 
                       new AppDataService();
        timerService = Application.Current?.Handler?.MauiContext?.Services.GetService<TimerService>() ?? 
                      new TimerService();
        roastDataService = Application.Current?.Handler?.MauiContext?.Services.GetService<RoastDataService>() ?? 
                          new RoastDataService(appDataService);
        beanService = Application.Current?.Handler?.MauiContext?.Services.GetService<BeanService>() ?? 
                     new BeanService(appDataService);
        preferencesService = Application.Current?.Handler?.MauiContext?.Services.GetService<PreferencesService>() ?? 
                            new PreferencesService();
        
        timerService.TimeUpdated += OnTimeUpdated;

        // Attach event handlers for weight entry text changes
        BatchWeightEntry.TextChanged += OnWeightEntryTextChanged;
        FinalWeightEntry.TextChanged += OnWeightEntryTextChanged;
        
        // Load available beans for the picker
        LoadBeans();
        
        // Handle bean selection changes
        BeanPicker.SelectedIndexChanged += BeanPicker_SelectedIndexChanged;
        
        // Initialize the data file path
        InitializeDataFilePath();
    }
    
    private async void InitializeDataFilePath()
    {
        try
        {
            // Check if user has a saved file path preference
            string savedFilePath = await preferencesService.GetAppDataFilePathAsync();
            
            if (!string.IsNullOrEmpty(savedFilePath))
            {
                // If file exists, use it
                if (File.Exists(savedFilePath))
                {
                    appDataService.SetCustomFilePath(savedFilePath);
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to initialize data file: {ex.Message}", "OK");
        }
    }
    
    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Refresh beans when returning to this page
        LoadBeans();
        // Re-initialize data file path in case it was changed in another page
        InitializeDataFilePath();
    }
    
    private async void LoadBeans()
    {
        try
        {
            // Get available beans (only ones with remaining quantity > 0)
            availableBeans = await beanService.GetAvailableBeansAsync();
            
            // Clear existing picker items
            BeanPicker.Items.Clear();
            
            if (availableBeans.Count == 0)
            {
                // No beans available
                BeanPicker.Items.Add("No beans - Add beans first");
                BeanPicker.SelectedIndex = 0;
                BeanPicker.IsEnabled = false;
                selectedBean = null;
                return;
            }
            
            BeanPicker.IsEnabled = true;
            
            // Add beans to picker
            foreach (var bean in availableBeans)
            {
                BeanPicker.Items.Add(bean.DisplayName);
            }
            
            // Select first item by default if nothing was selected before
            if (BeanPicker.SelectedIndex == -1 && BeanPicker.Items.Count > 0)
            {
                BeanPicker.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load beans: {ex.Message}", "OK");
        }
    }
    
    private void BeanPicker_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (BeanPicker.SelectedIndex >= 0 && BeanPicker.SelectedIndex < availableBeans.Count)
        {
            selectedBean = availableBeans[BeanPicker.SelectedIndex];
        }
        else
        {
            selectedBean = null;
        }
    }
    
    private async void ManageBeans_Clicked(object sender, EventArgs e)
    {
        // Navigate to the BeanInventoryPage using Shell navigation
        await Shell.Current.GoToAsync(nameof(BeanInventoryPage));
    }

    private bool ValidateInputs(out double batchWeight, out double finalWeight, out double temperature, 
        out int roastMinutes, out int roastSeconds)
    {
        batchWeight = 0;
        finalWeight = 0;
        temperature = 0;
        roastMinutes = 0;
        roastSeconds = 0;

        // Validate bean selection
        if (selectedBean == null)
        {
            DisplayAlert("Validation Error", "Please select a bean type or add beans to your inventory.", "OK");
            return false;
        }

        // Validate batch weight
        if (string.IsNullOrWhiteSpace(BatchWeightEntry.Text) ||
            !double.TryParse(BatchWeightEntry.Text, out batchWeight) ||
            batchWeight <= 0)
        {
            DisplayAlert("Validation Error", "Please enter a valid batch weight in grams.", "OK");
            return false;
        }
        
        // Check if enough beans are available (convert grams to kg)
        double batchWeightKg = batchWeight / 1000.0;
        if (batchWeightKg > selectedBean.RemainingQuantity)
        {
            DisplayAlert("Validation Error", 
                         $"Not enough beans available. You have {selectedBean.RemainingQuantity:F2}kg remaining, but need {batchWeightKg:F2}kg.", 
                         "OK");
            return false;
        }

        // Validate final weight
        if (string.IsNullOrWhiteSpace(FinalWeightEntry.Text) ||
            !double.TryParse(FinalWeightEntry.Text, out finalWeight) ||
            finalWeight <= 0)
        {
            DisplayAlert("Validation Error", "Please enter a valid final weight in grams.", "OK");
            return false;
        }

        // Ensure final weight is not greater than batch weight
        if (finalWeight > batchWeight)
        {
            DisplayAlert("Validation Error", "Final weight cannot be greater than batch weight.", "OK");
            return false;
        }

        // Validate temperature
        if (string.IsNullOrWhiteSpace(TemperatureEntry.Text) ||
            !double.TryParse(TemperatureEntry.Text, out temperature) ||
            temperature <= 0)
        {
            DisplayAlert("Validation Error", "Please enter a valid temperature in Celsius.", "OK");
            return false;
        }

        // Parse and validate time
        if (!ParseTimeEntry(out roastMinutes, out roastSeconds))
        {
            DisplayAlert("Validation Error", "Please enter a valid roast time.", "OK");
            return false;
        }

        // Ensure at least some time is entered
        if (roastMinutes == 0 && roastSeconds == 0)
        {
            DisplayAlert("Validation Error", "Roasting time must be greater than 0.", "OK");
            return false;
        }

        return true;
    }

    private bool ParseTimeEntry(out int minutes, out int seconds)
    {
        minutes = 0;
        seconds = 0;

        string timeText = TimeEntry.Text?.Trim() ?? "";
        
        // Check for MM:SS format
        string[] parts = timeText.Split(':');
        if (parts.Length == 2)
        {
            if (!int.TryParse(parts[0], out minutes) || 
                !int.TryParse(parts[1], out seconds))
            {
                return false;
            }
            
            if (seconds >= 60)
            {
                // Convert excess seconds to minutes
                minutes += seconds / 60;
                seconds = seconds % 60;
            }
            
            return true;
        }
        
        return false;
    }

    private string GetRoastLevel(double lossPercentage)
    {
        // Classify roast level based on weight loss percentage
        if (lossPercentage < 12.0)
            return "Light";
        else if (lossPercentage < 14.0)
            return "Medium-Light";
        else if (lossPercentage < 16.0)
            return "Medium";
        else if (lossPercentage < 18.0)
            return "Medium-Dark";
        else
            return "Dark";
    }

    private string FormatRoastTime(int minutes, int seconds)
    {
        if (minutes > 0 && seconds > 0)
            return $"{minutes}m {seconds}s";
        else if (minutes > 0)
            return $"{minutes}m";
        else
            return $"{seconds}s";
    }

    private void OnTimeUpdated(TimeSpan elapsedTime)
    {
        if (!isTimerUpdating)
        {
            isTimerUpdating = true;
            // Update both the invisible entry and the display label
            TimeEntry.Text = $"{elapsedTime.Minutes:D2}:{elapsedTime.Seconds:D2}";
            TimeDisplayLabel.Text = $"{elapsedTime.Minutes:D2}:{elapsedTime.Seconds:D2}";
            isTimerUpdating = false;
        }
    }

    // When time entry is focused, clear the field for easier input
    private void TimeEntry_Focused(object sender, FocusEventArgs e)
    {
        if (sender is Entry entry && e.IsFocused)
        {
            // Clear the field to allow fresh input
            isTimerUpdating = true;
            entry.Text = "";
            // Keep the display showing the current time
            isTimerUpdating = false;
        }
    }

    private void TimeEntry_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (isTimerUpdating)
            return;

        string text = e.NewTextValue ?? "";
        
        // Extract only the digits from input
        string inputDigits = new string(text.Where(char.IsDigit).ToArray());
        
        // Store digits in our buffer for natural typing experience
        if (!string.IsNullOrEmpty(inputDigits))
        {
            temporaryDigitsBuffer = inputDigits;
            
            // Format the display in real-time based on how many digits were entered
            UpdateTimeDisplayFromDigits(temporaryDigitsBuffer);
            return;
        }
        else if (string.IsNullOrEmpty(text) || !text.Contains(':'))
        {
            // Keep the field empty while user is typing
            temporaryDigitsBuffer = "";
            return;
        }
    }
    
    // Helper method to update the display based on digit input
    private void UpdateTimeDisplayFromDigits(string digits)
    {
        if (string.IsNullOrEmpty(digits))
        {
            TimeDisplayLabel.Text = "00:00";
            return;
        }
        
        switch (digits.Length)
        {
            case 1: // Single digit = seconds (units)
                TimeDisplayLabel.Text = $"00:0{digits}";
                break;
            case 2: // Two digits = seconds (tens + units)
                TimeDisplayLabel.Text = $"00:{digits}";
                break;
            case 3: // Three digits = 1 minute + seconds
                TimeDisplayLabel.Text = $"0{digits[0]}:{digits.Substring(1)}";
                break;
            case 4: // Four digits = minutes + seconds
                TimeDisplayLabel.Text = $"{digits.Substring(0, 2)}:{digits.Substring(2)}";
                break;
            default: // More than 4 digits - take last 4
                string lastFour = digits.Substring(digits.Length - 4);
                TimeDisplayLabel.Text = $"{lastFour.Substring(0, 2)}:{lastFour.Substring(2)}";
                break;
        }
    }
    
    // Format time when the field loses focus
    private void TimeEntry_Unfocused(object sender, FocusEventArgs e)
    {
        if (isTimerUpdating)
            return;
            
        // Now apply the formatting using the buffered digits
        if (!string.IsNullOrEmpty(temporaryDigitsBuffer))
        {
            isTimerUpdating = true;
            
            // Format based on how many digits were entered
            switch (temporaryDigitsBuffer.Length)
            {
                case 1: // Single digit = seconds (units)
                    TimeEntry.Text = $"00:0{temporaryDigitsBuffer}";
                    TimeDisplayLabel.Text = $"00:0{temporaryDigitsBuffer}";
                    break;
                case 2: // Two digits = seconds (tens + units)
                    TimeEntry.Text = $"00:{temporaryDigitsBuffer}";
                    TimeDisplayLabel.Text = $"00:{temporaryDigitsBuffer}";
                    break;
                case 3: // Three digits = 1 minute + seconds
                    TimeEntry.Text = $"0{temporaryDigitsBuffer[0]}:{temporaryDigitsBuffer.Substring(1)}";
                    TimeDisplayLabel.Text = $"0{temporaryDigitsBuffer[0]}:{temporaryDigitsBuffer.Substring(1)}";
                    break;
                case 4: // Four digits = minutes + seconds
                    TimeEntry.Text = $"{temporaryDigitsBuffer.Substring(0, 2)}:{temporaryDigitsBuffer.Substring(2)}";
                    TimeDisplayLabel.Text = $"{temporaryDigitsBuffer.Substring(0, 2)}:{temporaryDigitsBuffer.Substring(2)}";
                    break;
                default: // More than 4 digits - take last 4
                    string lastFour = temporaryDigitsBuffer.Substring(temporaryDigitsBuffer.Length - 4);
                    TimeEntry.Text = $"{lastFour.Substring(0, 2)}:{lastFour.Substring(2)}";
                    TimeDisplayLabel.Text = $"{lastFour.Substring(0, 2)}:{lastFour.Substring(2)}";
                    break;
            }
            
            // Clear the buffer
            temporaryDigitsBuffer = "";
            isTimerUpdating = false;
        }
        else
        {
            // If no digits were entered, reset to 00:00
            isTimerUpdating = true;
            TimeEntry.Text = "00:00";
            TimeDisplayLabel.Text = "00:00";
            isTimerUpdating = false;
        }
        
        // Format properly if it contains a colon already
        string text = TimeEntry.Text;
        if (text.Contains(':'))
        {
            string[] parts = text.Split(':');
            if (parts.Length == 2 && 
                int.TryParse(parts[0], out int minutes) && 
                int.TryParse(parts[1], out int seconds))
            {
                // Handle seconds overflow
                if (seconds >= 60)
                {
                    minutes += seconds / 60;
                    seconds = seconds % 60;
                }
                
                isTimerUpdating = true;
                TimeEntry.Text = $"{minutes:D2}:{seconds:D2}";
                TimeDisplayLabel.Text = $"{minutes:D2}:{seconds:D2}";
                isTimerUpdating = false;
            }
        }
    }

    private void StartTimer_Clicked(object sender, EventArgs e)
    {
        timerService.Start();
        StartTimerButton.IsEnabled = false;
        PauseTimerButton.IsEnabled = true;
        StopTimerButton.IsEnabled = true;

        // Disable manual editing while timer is running
        TimeEntry.IsEnabled = false;
    }

    private void PauseTimer_Clicked(object sender, EventArgs e)
    {
        timerService.Stop();
        StartTimerButton.IsEnabled = true;
        PauseTimerButton.IsEnabled = false;
        
        // Re-enable manual editing when timer is paused
        TimeEntry.IsEnabled = true;
    }

    private void StopTimer_Clicked(object sender, EventArgs e)
    {
        timerService.Stop();
        
        // Reset button states
        StartTimerButton.IsEnabled = true;
        PauseTimerButton.IsEnabled = false;
        StopTimerButton.IsEnabled = false;
        
        // Re-enable manual editing when timer is stopped
        TimeEntry.IsEnabled = true;
    }

    private void ResetTimer_Clicked(object sender, EventArgs e)
    {
        timerService.Reset();
        
        isTimerUpdating = true;
        TimeEntry.Text = "00:00";
        TimeDisplayLabel.Text = "00:00";
        isTimerUpdating = false;
        
        StartTimerButton.IsEnabled = true;
        PauseTimerButton.IsEnabled = false;
        StopTimerButton.IsEnabled = false;
        
        // Re-enable manual editing when timer is reset
        TimeEntry.IsEnabled = true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        timerService.Stop();
    }

    private void OnWeightEntryTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (double.TryParse(BatchWeightEntry.Text, out double batchWeight) &&
            double.TryParse(FinalWeightEntry.Text, out double finalWeight) &&
            batchWeight > 0 && finalWeight > 0 && finalWeight <= batchWeight)
        {
            double lossWeight = batchWeight - finalWeight;
            double lossPercentage = (lossWeight / batchWeight) * 100;
            LossPercentLabel.Text = $"Weight Loss: {lossPercentage:F2}%";

            string roastLevel = GetRoastLevel(lossPercentage);
            RoastSummaryLabel.Text = $"Roast Summary: {roastLevel} roast at {TemperatureEntry.Text}°C";
        }
        else
        {
            LossPercentLabel.Text = "Weight Loss: --";
            RoastSummaryLabel.Text = "Roast Summary: --";
        }
    }

    // New Save button click handler
    private async void SaveRoast_Clicked(object sender, EventArgs e)
    {
        if (!ValidateInputs(out double batchWeight, out double finalWeight, out double temperature, 
                            out int roastMinutes, out int roastSeconds))
        {
            return; // Validation failed
        }

        try
        {
            // Create RoastData object
            var roastData = new RoastData
            {
                BeanType = selectedBean?.DisplayName ?? "Unknown",
                Temperature = temperature,
                BatchWeight = batchWeight,
                FinalWeight = finalWeight,
                RoastMinutes = roastMinutes,
                RoastSeconds = roastSeconds,
                RoastDate = DateTime.Now,
                Notes = NotesEditor.Text ?? ""
            };

            // Update bean inventory (reduce remaining quantity)
            if (selectedBean != null)
            {
                double batchWeightKg = batchWeight / 1000.0; // Convert g to kg
                await beanService.UpdateBeanQuantityAsync(selectedBean.Id, batchWeightKg);
            }

            // Save data using service
            bool success = await roastDataService.SaveRoastDataAsync(roastData);

            if (success)
            {
                await DisplayAlert("Success", "Roast data saved successfully!", "OK");

                // Refresh beans after saving (quantity has changed)
                LoadBeans();
                
                // Clear form for next entry
                ClearForm();
            }
            else
            {
                await DisplayAlert("Error", "Failed to save roast data. Please try again.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
        }
    }

    private async void ViewLogs_Clicked(object sender, EventArgs e)
    {
        // Navigate to the RoastLogPage using Shell navigation
        await Shell.Current.GoToAsync(nameof(RoastLogPage));
    }

    private void ClearForm()
    {
        // Reset timer
        ResetTimer_Clicked(this, EventArgs.Empty);
        
        // Reset form fields
        BatchWeightEntry.Text = string.Empty;
        FinalWeightEntry.Text = string.Empty;
        TemperatureEntry.Text = string.Empty;
        NotesEditor.Text = string.Empty;
        
        // Reset labels
        LossPercentLabel.Text = "Weight Loss: --";
        RoastSummaryLabel.Text = "Roast Summary: --";
    }

    // Future enhancement: Method to export data to CSV
    private async Task ExportDataAsync()
    {
        try
        {
            // Select a file location on the device
            var customFileType = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".csv" } },
                    { DevicePlatform.Android, new[] { "text/csv" } },
                    { DevicePlatform.iOS, new[] { "public.comma-separated-values-text" } },
                    { DevicePlatform.MacCatalyst, new[] { "public.comma-separated-values-text" } }
                });

            var options = new PickOptions
            {
                PickerTitle = "Select where to save CSV file",
                FileTypes = customFileType
            };

            var result = await FilePicker.PickAsync(options);
            if (result != null)
            {
                await roastDataService.ExportRoastLogAsync(result.FullPath);
                await DisplayAlert("Success", "Roast log exported successfully!", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to export data: {ex.Message}", "OK");
        }
    }
}

