using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text; // Add System.Text namespace for StringBuilder
using System.Threading.Tasks;
using CafeMaestro.Models;
using CafeMaestro.Services;
using System.IO; // Added for File.Exists
using Microsoft.Extensions.DependencyInjection;

namespace CafeMaestro;

[QueryProperty(nameof(EditRoastId), "EditRoastId")]
[QueryProperty(nameof(NewRoast), "NewRoast")]
public partial class RoastPage : ContentPage
{
    private TimerService timerService;
    private RoastDataService roastDataService;
    private BeanDataService beanService;
    private AppDataService appDataService;
    private PreferencesService preferencesService;
    private RoastLevelService roastLevelService;
    private bool isTimerUpdating = false; // Flag to prevent recursive updates
    private string temporaryDigitsBuffer = ""; // Store digits before formatting
    
    // First Crack tracking
    private int? firstCrackMinutes = null;
    private int? firstCrackSeconds = null;

    // Property for edit mode
    private Guid _editRoastId = Guid.Empty;
    private RoastData? _roastToEdit = null;
    private bool _isEditMode = false;
    
    // Property to force new roast mode
    private bool _isForceNewRoast = false;
    
    public string NewRoast
    {
        get => _isForceNewRoast.ToString();
        set
        {
            if (bool.TryParse(value, out bool isNew) && isNew)
            {
                _isForceNewRoast = true;
                System.Diagnostics.Debug.WriteLine("NewRoast property set to true - will force reset form");
                
                // Force a reset of the page for a new roast
                MainThread.BeginInvokeOnMainThread(() => {
                    ResetPageForNewRoast();
                });
            }
        }
    }

    public string EditRoastId
    {
        get => _editRoastId.ToString();
        set
        {
            if (Guid.TryParse(value, out Guid parsedId))
            {
                System.Diagnostics.Debug.WriteLine($"Setting EditRoastId to: {parsedId}");
                _editRoastId = parsedId;
                _isEditMode = true;
                
                // Delay the data loading to ensure the page is fully ready
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        // Reset any previous selections to avoid conflicts
                        selectedBean = null;
                        
                        // Clear and wait for page to be ready
                        await Task.Delay(50);
                        
                        System.Diagnostics.Debug.WriteLine($"Now loading data for edit ID: {_editRoastId}");
                        await Task.Run(() => LoadRoastDataForEdit());
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in delayed LoadRoastDataForEdit: {ex.Message}");
                    }
                });
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Invalid roast ID format: {value}");
            }
        }
    }

    // Store the selected bean for roasting
    private BeanData? selectedBean = null;
    private List<BeanData> availableBeans = new List<BeanData>();
    
    // Previous roast tracking
    private RoastData? _previousRoast = null;
    public bool HasPreviousRoast => _previousRoast != null;

    private CancellationTokenSource? _animationCancellationTokenSource;
    private CancellationTokenSource? _cursorAnimationCancellationTokenSource;

    public RoastPage(TimerService? timerService = null, RoastDataService? roastDataService = null,
                    BeanDataService? beanService = null, AppDataService? appDataService = null,
                    PreferencesService? preferencesService = null)
    {
        InitializeComponent();

        // First try to get the services from the application resources (our stored service provider)
        if (Application.Current?.Resources.TryGetValue("ServiceProvider", out var serviceProviderObj) == true &&
            serviceProviderObj is IServiceProvider serviceProvider)
        {
            this.appDataService = appDataService ??
                                 serviceProvider.GetService<AppDataService>() ??
                                 Application.Current?.Handler?.MauiContext?.Services.GetService<AppDataService>() ??
                                 throw new InvalidOperationException("AppDataService not available");

            this.timerService = timerService ??
                               serviceProvider.GetService<TimerService>() ??
                               Application.Current?.Handler?.MauiContext?.Services.GetService<TimerService>() ??
                               throw new InvalidOperationException("TimerService not available");

            this.roastDataService = roastDataService ??
                                   serviceProvider.GetService<RoastDataService>() ??
                                   Application.Current?.Handler?.MauiContext?.Services.GetService<RoastDataService>() ??
                                   throw new InvalidOperationException("RoastDataService not available");

            this.beanService = beanService ??
                              serviceProvider.GetService<BeanDataService>() ??
                              Application.Current?.Handler?.MauiContext?.Services.GetService<BeanDataService>() ??
                              throw new InvalidOperationException("BeanService not available");

            this.preferencesService = preferencesService ??
                                     serviceProvider.GetService<PreferencesService>() ??
                                     Application.Current?.Handler?.MauiContext?.Services.GetService<PreferencesService>() ??
                                     throw new InvalidOperationException("PreferencesService not available");

            this.roastLevelService = serviceProvider.GetService<RoastLevelService>() ??
                                     Application.Current?.Handler?.MauiContext?.Services.GetService<RoastLevelService>() ??
                                     throw new InvalidOperationException("RoastLevelService not available");
        }
        else
        {
            // Fall back to the old way if app resources doesn't have our provider
            this.appDataService = appDataService ??
                                 Application.Current?.Handler?.MauiContext?.Services.GetService<AppDataService>() ??
                                 throw new InvalidOperationException("AppDataService not available");

            this.timerService = timerService ??
                               Application.Current?.Handler?.MauiContext?.Services.GetService<TimerService>() ??
                               throw new InvalidOperationException("TimerService not available");

            this.roastDataService = roastDataService ??
                                   Application.Current?.Handler?.MauiContext?.Services.GetService<RoastDataService>() ??
                                   throw new InvalidOperationException("RoastDataService not available");

            this.beanService = beanService ??
                              Application.Current?.Handler?.MauiContext?.Services.GetService<BeanDataService>() ??
                              throw new InvalidOperationException("BeanDataService not available");

            this.preferencesService = preferencesService ??
                                     Application.Current?.Handler?.MauiContext?.Services.GetService<PreferencesService>() ??
                                     throw new InvalidOperationException("PreferencesService not available");

            this.roastLevelService = Application.Current?.Handler?.MauiContext?.Services.GetService<RoastLevelService>() ??
                                     throw new InvalidOperationException("RoastLevelService not available");
        }

        System.Diagnostics.Debug.WriteLine($"RoastPage constructor - Using AppDataService at path: {this.appDataService.DataFilePath}");

        this.timerService.TimeUpdated += OnTimeUpdated;

        // Attach event handlers for weight entry text changes
        BatchWeightEntry.TextChanged += OnWeightEntryTextChanged;
        FinalWeightEntry.TextChanged += OnWeightEntryTextChanged;
        
        // Attach event handler for batch weight validation
        BatchWeightEntry.TextChanged += BatchWeightEntry_TextChanged;

        // Handle bean selection changes
        BeanPicker.SelectedIndexChanged += BeanPicker_SelectedIndexChanged;
    }

    private async void InitializeDataFilePath()
    {
        try
        {
            // Check if this is the first run
            bool isFirstRun = await preferencesService.IsFirstRunAsync();

            // Check if user has a saved file path preference
            string? savedFilePath = await preferencesService.GetAppDataFilePathAsync();

            if (!string.IsNullOrEmpty(savedFilePath))
            {
                // If file exists, use it
                if (File.Exists(savedFilePath))
                {
                    // Use the async version of SetCustomFilePath
                    await appDataService.SetCustomFilePathAsync(savedFilePath);

                    // Also initialize related services with the same path
                    await roastDataService.InitializeFromPreferencesAsync(preferencesService);
                    await beanService.InitializeFromPreferencesAsync(preferencesService);
                }
                else
                {
                    // File doesn't exist anymore
                    await DisplayAlert("Data File Not Found",
                        $"The previously used data file could not be found: {savedFilePath}\n\nUsing default location instead.",
                        "OK");

                    // Reset to default path - use the async version
                    await appDataService.ResetToDefaultPathAsync();
                    await preferencesService.ClearAppDataFilePathAsync();
                }
            }
            else if (isFirstRun)
            {
                // First run with no saved path - prompt user to set up data file
                // This shouldn't normally happen as App.xaml.cs should handle first run,
                // but handle it as a fallback
                await Shell.Current.GoToAsync(nameof(SettingsPage));

                await DisplayAlert("Welcome to CafeMaestro",
                    "Please select or create a data file location to store your coffee roasting data.",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to initialize data file: {ex.Message}", "OK");
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Track the current edit ID before potentially making changes that affect state
        Guid currentEditId = _editRoastId;
        
        // Check if we're being navigated to from the tab bar
        bool isFromTabBar = CheckIfNavigationFromTabBar();
        
        System.Diagnostics.Debug.WriteLine($"RoastPage.OnAppearing - IsEditMode: {_isEditMode}, IsFromTabBar: {isFromTabBar}, EditId: {currentEditId}, ForceNew: {_isForceNewRoast}");

        // If we're coming from the tab bar or the force new flag is set, and we're not in edit mode, reset the form
        if ((isFromTabBar || _isForceNewRoast) && currentEditId == Guid.Empty)
        {
            System.Diagnostics.Debug.WriteLine("RoastPage appearing - resetting form for new roast");
            ResetPageForNewRoast();
            ClearForm(); // Make sure we call ClearForm explicitly
            
            // Reset First Crack tracking
            ResetFirstCrackTracking();
        }
        else if (isFromTabBar && currentEditId != Guid.Empty)
        {
            // Coming from tab bar with an edit ID - make sure we preserve it
            System.Diagnostics.Debug.WriteLine($"RoastPage appearing from tab bar with edit ID: {currentEditId} - preserving edit mode");
        }
        else if (!_isEditMode)
        {
            // Always reset the form if we're not in edit mode, regardless of the navigation source
            System.Diagnostics.Debug.WriteLine("RoastPage appearing without edit mode - forcing form reset");
            ResetPageForNewRoast();
            ClearForm(); // Make sure we call ClearForm explicitly
            
            // Reset First Crack tracking
            ResetFirstCrackTracking();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"RoastPage appearing in edit mode - keeping form data");
        }
        
        // Reset the force new flag
        _isForceNewRoast = false;

        // Refresh beans when returning to this page
        await LoadAvailableBeans();
        
        // Run validation on the batch weight (if any)
        ValidateBatchWeight();
    }

    // Method to determine if navigation is from tab bar
    private bool CheckIfNavigationFromTabBar()
    {
        try
        {
            // Get the current shell item
            if (Shell.Current?.CurrentItem != null)
            {
                var currentRoute = Shell.Current.CurrentItem.Route;
                System.Diagnostics.Debug.WriteLine($"Current route: {currentRoute}");

                // If the current route is "RoastPage", it's likely coming from the tab bar
                return currentRoute == "RoastPage";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking navigation source: {ex.Message}");
        }

        return false;
    }

    // Method to reset the page for a new roast
    private void ResetPageForNewRoast()
    {
        // Reset edit state
        _isEditMode = false;
        _roastToEdit = null;
        _editRoastId = Guid.Empty;

        // Reset the page title
        Title = "Roast Coffee";

        // Clear form fields
        ClearForm();
        
        // Reset First Crack tracking
        ResetFirstCrackTracking();
    }

    private async Task LoadAvailableBeans()
    {
        try
        {
            // Clear existing items first
            BeanPicker.Items.Clear();
            BeanPicker.Title = "Loading beans...";

            // Use MainThread to ensure UI updates happen on the UI thread
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                // Get available beans from service - use the sorted version which:
                // 1. Filters out beans with RemainingQuantity <= 0 
                // 2. Orders by purchase date (newest first) then by display name
                availableBeans = await beanService.GetSortedAvailableBeansAsync();

                // Log the count for debugging
                System.Diagnostics.Debug.WriteLine($"Loaded {availableBeans.Count} sorted available beans");

                if (availableBeans.Count > 0)
                {
                    // Clear the picker again in case any items were added while loading
                    BeanPicker.Items.Clear();

                    // Add each bean to the picker
                    foreach (var bean in availableBeans)
                    {
                        BeanPicker.Items.Add(bean.DisplayName);
                    }

                    // Update picker title
                    BeanPicker.Title = "Select Bean Type";

                    // Select first bean as default
                    BeanPicker.SelectedIndex = 0;
                    selectedBean = availableBeans[0];
                }
                else
                {
                    // No beans available
                    BeanPicker.Title = "No beans available";
                    selectedBean = null;
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading beans: {ex.Message}");

            // Update UI on main thread to show error
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                BeanPicker.Items.Clear();
                BeanPicker.Items.Add("Error loading beans");
                BeanPicker.Title = "Error loading beans";
                selectedBean = null;
            });

            // Display alert to user
            await DisplayAlert("Error", "Failed to load beans. Please try again.", "OK");
        }
    }

    private async void BeanPicker_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (BeanPicker.SelectedIndex >= 0 && BeanPicker.SelectedIndex < availableBeans.Count)
        {
            selectedBean = availableBeans[BeanPicker.SelectedIndex];
            
            // Look up previous roast of this bean type
            if (selectedBean != null)
            {
                await LoadPreviousRoastData(selectedBean.DisplayName);
                
                // Validate the batch weight against the newly selected bean's remaining quantity
                ValidateBatchWeight();
            }
        }
        else
        {
            selectedBean = null;
            // Clear previous roast data when no bean is selected
            _previousRoast = null;
            UpdatePreviousRoastDisplay();
            
            // Hide any validation warnings when no bean is selected
            BatchWeightWarningLabel.IsVisible = false;
        }
    }

    // Method to load previous roast data
    private async Task LoadPreviousRoastData(string beanType)
    {
        try
        {
            _previousRoast = await roastDataService.GetLastRoastForBeanTypeAsync(beanType);
            
            // Update the UI to show/hide previous roast info
            UpdatePreviousRoastDisplay();
            
            if (_previousRoast != null)
            {
                System.Diagnostics.Debug.WriteLine($"Loaded previous roast: {_previousRoast.Summary}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading previous roast: {ex.Message}");
            _previousRoast = null;
            // Update UI to hide previous roast section
            UpdatePreviousRoastDisplay();
        }
    }
    
    // Method to update the previous roast display
    private void UpdatePreviousRoastDisplay()
    {
        MainThread.BeginInvokeOnMainThread(() => {
            // Only show previous roast section if we have data
            PreviousRoastInfoSection.IsVisible = HasPreviousRoast;
            
            if (HasPreviousRoast && _previousRoast != null)
            {
                // Format date to a user-friendly format
                string dateText = _previousRoast.RoastDate.ToString("MM/dd/yyyy");
                
                // First line: Date and summary
                PreviousRoastSummaryLabel.Text = $"{dateText}: {_previousRoast.RoastLevelName} roast";
                
                // Second line: Details
                StringBuilder details = new StringBuilder();
                details.Append($"Batch: {_previousRoast.BatchWeight:F1}g | ");
                details.Append($"Temp: {_previousRoast.Temperature}°C | ");
                details.Append($"Time: {_previousRoast.FormattedTime} | ");
                details.Append($"Loss: {_previousRoast.WeightLossPercentage:F1}%");
                
                // Add first crack info if available
                if (_previousRoast.FirstCrackSeconds.HasValue)
                {
                    details.Append($" | First Crack: {_previousRoast.FirstCrackTime}");
                }
                
                PreviousRoastDetailsLabel.Text = details.ToString();
                
                // If we need to prefill form fields, do it here - will be handled separately
                PrefillFieldsFromPreviousRoast();
            }
            else
            {
                PreviousRoastSummaryLabel.Text = "";
                PreviousRoastDetailsLabel.Text = "";
            }
        });
    }
    
    // New method to handle batch weight validation
    private void BatchWeightEntry_TextChanged(object? sender, TextChangedEventArgs e)
    {
        ValidateBatchWeight();
    }
    
    // Method to validate batch weight against available bean quantity
    private void ValidateBatchWeight()
    {
        try
        {
            // Skip validation if no bean is selected or no weight entered
            if (selectedBean == null || string.IsNullOrWhiteSpace(BatchWeightEntry.Text))
            {
                // Hide warning and ensure timer start button is enabled
                BatchWeightWarningLabel.IsVisible = false;
                StartTimerButton.IsEnabled = true;
                return;
            }
            
            // Parse batch weight and validate against remaining quantity
            if (double.TryParse(BatchWeightEntry.Text, out double batchWeight) && batchWeight > 0)
            {
                // Calculate available beans in grams (convert kg to g)
                double availableBeans = selectedBean.RemainingQuantity * 1000.0;
                
                // Check if batch weight exceeds available quantity
                if (batchWeight > availableBeans)
                {
                    // Show warning and disable timer start button
                    BatchWeightWarningLabel.Text = $"Insufficient beans available! (only {availableBeans:F1} g remaining)";
                    BatchWeightWarningLabel.IsVisible = true;
                    StartTimerButton.IsEnabled = false;
                    
                    System.Diagnostics.Debug.WriteLine($"Batch weight validation failed: {batchWeight}g exceeds available {availableBeans}g");
                }
                else
                {
                    // Clear warning and enable timer start button
                    BatchWeightWarningLabel.IsVisible = false;
                    StartTimerButton.IsEnabled = true;
                    
                    System.Diagnostics.Debug.WriteLine($"Batch weight validation passed: {batchWeight}g <= {availableBeans}g available");
                }
            }
            else
            {
                // Invalid or zero weight - clear warning but don't enable timer button
                BatchWeightWarningLabel.IsVisible = false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in batch weight validation: {ex.Message}");
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

    private async Task<string> GetRoastLevel(double lossPercentage)
    {
        try
        {
            return await roastLevelService.GetRoastLevelNameAsync(lossPercentage);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting roast level: {ex.Message}");
            return "Unknown";
        }
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

    private void TimeEntry_Focused(object sender, FocusEventArgs e)
    {
        if (sender is Entry entry && e.IsFocused)
        {
            // Clear the field to allow fresh input
            isTimerUpdating = true;
            entry.Text = "";
            // Keep the display showing the current time
            isTimerUpdating = false;

            // Start the timer edit animation - the whole display will blink
            StartTimerEditAnimation();

            // Add keyboard event handlers to detect Enter key
            entry.Completed += TimeEntry_Completed;
        }
    }

    private void TimeEntry_Completed(object? sender, EventArgs e)
    {
        // This is triggered when user presses Enter
        if (sender is Entry entry)
        {
            // Explicitly unfocus the entry to complete editing
            entry.Unfocus();

            // Remove the handler to avoid memory leaks
            entry.Completed -= TimeEntry_Completed;

            // Explicitly stop the animation
            StopTimerEditAnimation();
        }
    }

    // Format time when the field loses focus
    private void TimeEntry_Unfocused(object sender, FocusEventArgs e)
    {
        if (isTimerUpdating)
            return;

        // Stop the timer edit animation when unfocused
        StopTimerEditAnimation();

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

    private void StartTimer_Clicked(object sender, EventArgs e)
    {
        timerService.Start();
        StartTimerButton.IsEnabled = false;
        PauseTimerButton.IsEnabled = true;
        StopTimerButton.IsEnabled = true;

        // Disable manual editing while timer is running
        TimeEntry.IsEnabled = false;
        
        // Update First Crack button state based on timer running
        UpdateFirstCrackButtonState(true);

        // Show and animate the timer running indicator
        TimerRunningIndicator.IsVisible = true;
        StartTimerPulseAnimation();
    }

    private void PauseTimer_Clicked(object sender, EventArgs e)
    {
        timerService.Stop();
        StartTimerButton.IsEnabled = true;
        PauseTimerButton.IsEnabled = false;

        // Re-enable manual editing when timer is paused
        TimeEntry.IsEnabled = true;
        
        // Update First Crack button state when timer is paused
        UpdateFirstCrackButtonState(false);

        // Stop the pulse animation when timer is paused
        StopTimerPulseAnimation();
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
        
        // Update First Crack button state when timer is stopped
        UpdateFirstCrackButtonState(false);

        // Stop the pulse animation when timer is stopped
        StopTimerPulseAnimation();
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
        
        // Reset First Crack tracking when timer is reset
        ResetFirstCrackTracking();

        // Stop the pulse animation when timer is reset
        StopTimerPulseAnimation();
    }

    private void StartTimerPulseAnimation()
    {
        // Make sure the indicator is visible
        TimerRunningIndicator.IsVisible = true;

        // Cancel any existing animation
        _animationCancellationTokenSource?.Cancel();
        _animationCancellationTokenSource = new CancellationTokenSource();

        // Start a new animation task
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var token = _animationCancellationTokenSource.Token;

                // Continue animation until canceled
                while (!token.IsCancellationRequested)
                {
                    // Toggle between visible and invisible to create blinking effect
                    TimerRunningIndicator.IsVisible = true;
                    await Task.Delay(500, token); // Visible for 500ms

                    if (token.IsCancellationRequested) break;

                    TimerRunningIndicator.IsVisible = false;
                    await Task.Delay(500, token); // Invisible for 500ms
                }
            }
            catch (TaskCanceledException)
            {
                // This is expected when canceling the animation
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Animation error: {ex.Message}");
            }
        });
    }

    private void StopTimerPulseAnimation()
    {
        // Cancel the animation
        _animationCancellationTokenSource?.Cancel();
        _animationCancellationTokenSource = null;

        // Ensure indicator is hidden
        TimerRunningIndicator.IsVisible = false;
    }

    private void StartTimerEditAnimation()
    {
        // Cancel any existing animation
        _cursorAnimationCancellationTokenSource?.Cancel();
        _cursorAnimationCancellationTokenSource = new CancellationTokenSource();

        // Store the original color for restoration later
        var originalColor = TimeDisplayLabel.TextColor;

        // Start a new animation task
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var token = _cursorAnimationCancellationTokenSource.Token;

                // Continue animation until canceled
                while (!token.IsCancellationRequested)
                {
                    // Toggle between normal and dimmed color to create blinking effect
                    TimeDisplayLabel.TextColor = originalColor;
                    await Task.Delay(500, token); // Normal for 500ms

                    if (token.IsCancellationRequested) break;

                    // Set to a semi-transparent version of the color
                    if (originalColor is Microsoft.Maui.Graphics.Color color)
                    {
                        TimeDisplayLabel.TextColor = color.WithAlpha(0.3f);
                    }
                    else
                    {
                        // Fallback if we can't get the color
                        TimeDisplayLabel.Opacity = 0.3;
                    }
                    await Task.Delay(500, token); // Dimmed for 500ms

                    if (token.IsCancellationRequested) break;

                    // Restore normal opacity if we changed it
                    if (!(originalColor is Microsoft.Maui.Graphics.Color))
                    {
                        TimeDisplayLabel.Opacity = 1.0;
                    }
                }
            }
            catch (TaskCanceledException)
            {
                // This is expected when canceling the animation
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Timer edit animation error: {ex.Message}");
            }
            finally
            {
                // Ensure the display returns to normal
                TimeDisplayLabel.TextColor = originalColor;
                TimeDisplayLabel.Opacity = 1.0;
            }
        });
    }

    private void StopTimerEditAnimation()
    {
        try
        {
            // Cancel the animation - ensure we clean up properly
            if (_cursorAnimationCancellationTokenSource != null)
            {
                _cursorAnimationCancellationTokenSource.Cancel();
                _cursorAnimationCancellationTokenSource.Dispose();
                _cursorAnimationCancellationTokenSource = null;
            }

            // Force immediate reset of display properties
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Ensure the display returns to normal by explicitly setting the values
                if (Application.Current?.Resources != null &&
                    Application.Current.Resources.TryGetValue("PrimaryColor", out var colorObj) &&
                    colorObj is Microsoft.Maui.Graphics.Color color)
                {
                    TimeDisplayLabel.TextColor = color;
                }
                TimeDisplayLabel.Opacity = 1.0;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error stopping timer edit animation: {ex.Message}");
            // Ensure the display is reset even if there's an error
            if (TimeDisplayLabel != null)
            {
                TimeDisplayLabel.Opacity = 1.0;
            }
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Stop the timer when leaving the page
        timerService.Stop();
        
        // Store whether we were in edit mode before completing
        bool wasInEditMode = _isEditMode;
        
        // Reset the edit flags but keep the ID temporarily for debugging
        Guid previousEditId = _editRoastId;
        
        // Reset edit mode flags but don't clear form data yet
        _isEditMode = false;
        _roastToEdit = null;
        _editRoastId = Guid.Empty;
        
        System.Diagnostics.Debug.WriteLine($"RoastPage.OnDisappearing - Cleared edit mode. Was editing: {wasInEditMode}, ID: {previousEditId}");
    }

    private void OnWeightEntryTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (double.TryParse(BatchWeightEntry.Text, out double batchWeight) &&
            double.TryParse(FinalWeightEntry.Text, out double finalWeight) &&
            batchWeight > 0 && finalWeight > 0 && finalWeight <= batchWeight)
        {
            double lossWeight = batchWeight - finalWeight;
            double lossPercentage = (lossWeight / batchWeight) * 100;
            string roastLevel = GetRoastLevel(lossPercentage).Result;
            LossPercentLabel.Text = $"Weight loss {lossPercentage:F1}% ({roastLevel} roast)";
        }
    }

    // Add this method to handle the First Crack button click
    private void FirstCrackButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            // Get current time from timer service
            TimeSpan currentTime = timerService.GetElapsedTime();
            
            // Store first crack time
            firstCrackMinutes = currentTime.Minutes;
            firstCrackSeconds = currentTime.Seconds;
            
            // Update UI
            FirstCrackLabel.Text = $"First Crack: {firstCrackMinutes:D2}:{firstCrackSeconds:D2}";
            
            // Disable the button to prevent multiple presses
            FirstCrackButton.IsEnabled = false;
            
            // Log the first crack event
            System.Diagnostics.Debug.WriteLine($"First Crack marked at {firstCrackMinutes:D2}:{firstCrackSeconds:D2}");
        }
        catch (Exception ex)
        {
            // Provide a more descriptive error message
            System.Diagnostics.Debug.WriteLine($"Error marking First Crack: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            
            // Ensure UI is reset if there's an error
            ResetFirstCrackTracking();
            
            // Display an error message to the user
            MainThread.BeginInvokeOnMainThread(async () => {
                await DisplayAlert("Error", "Failed to mark First Crack. Please try again.", "OK");
            });
        }
    }
    
    // Method to reset First Crack tracking
    private void ResetFirstCrackTracking()
    {
        firstCrackMinutes = null;
        firstCrackSeconds = null;
        
        // Reset UI elements
        FirstCrackLabel.Text = "First Crack: Not marked";
        FirstCrackButton.IsEnabled = false;
        
        // Reset the button's background color to the application's accent color.
        // This ensures the button visually aligns with the app's theme after being reset.
        if (Application.Current?.Resources != null &&
            Application.Current.Resources.TryGetValue("AccentColor", out var colorObj) &&
            colorObj is Microsoft.Maui.Graphics.Color color)
        {
            FirstCrackButton.BackgroundColor = color;
        }
    }

    // Helper method to update FirstCrackButton state based on timer and crack status
    private void UpdateFirstCrackButtonState(bool isTimerRunning)
    {
        // Only enable the First Crack button when the timer is running and first crack hasn't been marked yet
        FirstCrackButton.IsEnabled = isTimerRunning && !firstCrackSeconds.HasValue;
    }

    // Save button click handler - handles both new roasts and editing existing ones
    private async void SaveRoast_Clicked(object sender, EventArgs e)
    {
        if (!ValidateInputs(out double batchWeight, out double finalWeight, out double temperature,
                            out int roastMinutes, out int roastSeconds))
        {
            return; // Validation failed
        }

        try
        {
            // Track whether this is a new roast or an update
            bool isUpdatingExisting = _isEditMode && _roastToEdit != null;
            bool success = false;

            if (isUpdatingExisting)
            {
                // Update existing roast data
                System.Diagnostics.Debug.WriteLine($"Updating existing roast with ID: {_roastToEdit!.Id}");

                // Create an updated roast object (keeping the original ID and date)
                var updatedRoast = new RoastData
                {
                    Id = _roastToEdit.Id,
                    RoastDate = _roastToEdit.RoastDate,
                    BeanType = selectedBean?.DisplayName ?? _roastToEdit.BeanType,
                    Temperature = temperature,
                    BatchWeight = batchWeight,
                    FinalWeight = finalWeight,
                    RoastMinutes = roastMinutes,
                    RoastSeconds = roastSeconds,
                    Notes = NotesEditor.Text ?? _roastToEdit.Notes,
                    FirstCrackMinutes = firstCrackMinutes,
                    FirstCrackSeconds = firstCrackSeconds
                    // RoastLevelName will be set in UpdateRoastLogAsync
                };
                
                // Use the service to update the roast
                success = await roastDataService.UpdateRoastLogAsync(updatedRoast);
                
                if (success)
                {
                    await DisplayAlert("Success", "Roast data updated successfully!", "OK");

                    // Navigate back to RoastLogPage when done with edit
                    await Shell.Current.GoToAsync("//RoastLogPage");
                }
                else
                {
                    await DisplayAlert("Error", "Failed to update roast data. Please try again.", "OK");
                }
            }
            else
            {
                // Create a new RoastData object
                var roastData = new RoastData
                {
                    Id = Guid.NewGuid(),
                    BeanType = selectedBean?.DisplayName ?? "Unknown",
                    Temperature = temperature,
                    BatchWeight = batchWeight,
                    FinalWeight = finalWeight,
                    RoastMinutes = roastMinutes,
                    RoastSeconds = roastSeconds,
                    RoastDate = DateTime.Now,
                    Notes = NotesEditor.Text ?? "",
                    FirstCrackMinutes = firstCrackMinutes,
                    FirstCrackSeconds = firstCrackSeconds
                    // RoastLevelName will be set in SaveRoastDataAsync
                };
                
                System.Diagnostics.Debug.WriteLine($"Creating new roast for {roastData.BeanType}");

                // Update bean inventory (reduce remaining quantity)
                if (selectedBean != null)
                {
                    double batchWeightKg = batchWeight / 1000.0; // Convert g to kg
                    await beanService.UpdateBeanQuantityAsync(selectedBean.Id, batchWeightKg);
                }

                // Save new data using service
                success = await roastDataService.SaveRoastDataAsync(roastData);

                if (success)
                {
                    await DisplayAlert("Success", "Roast data saved successfully!", "OK");

                    // Refresh beans after saving (quantity has changed)
                    await LoadAvailableBeans();

                    // Clear form for next entry
                    ClearForm();
                }
                else
                {
                    await DisplayAlert("Error", "Failed to save roast data. Please try again.", "OK");
                }
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
        LossPercentLabel.Text = "";
        
        // Reset First Crack tracking
        ResetFirstCrackTracking();
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

    // Override OnBackButtonPressed to handle Android back button
    protected override bool OnBackButtonPressed()
    {
        // When back button is pressed, navigate to MainPage
        try
        {
            // Navigate back to MainPage using direct Shell.CurrentItem assignment
            // This works better on Android than GoToAsync
            if (Shell.Current?.Items.Count > 0)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Shell.Current.CurrentItem = Shell.Current.Items[0]; // MainPage is the first item
                    System.Diagnostics.Debug.WriteLine("Navigated back to MainPage using hardware back button in RoastPage");
                });
                return true; // Indicate we've handled the back button
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling back button in RoastPage: {ex.Message}");
        }

        return base.OnBackButtonPressed(); // Let the system handle it if our code fails
    }

    // Method to load roast data when in edit mode
    private async void LoadRoastDataForEdit()
    {
        try
        {
            if (_editRoastId == Guid.Empty)
            {
                System.Diagnostics.Debug.WriteLine("LoadRoastDataForEdit called but edit ID is empty");
                return; // Not in edit mode
            }

            // Use the RoastDataService to get the specific roast by ID
            _roastToEdit = await roastDataService.GetRoastLogByIdAsync(_editRoastId);

            if (_roastToEdit == null)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to find roast with ID: {_editRoastId}");
                await DisplayAlert("Error", "Failed to find the roast data for editing", "OK");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"Found roast to edit: {_roastToEdit.BeanType} from {_roastToEdit.RoastDate}");

            // Update the UI with the roast data - ensure we await this call
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    // Set page title to indicate editing mode
                    Title = "Edit Roast";

                    // Make sure we're in edit mode
                    _isEditMode = true;

                    // Load beans first to ensure the picker is populated before we try to select an item
                    await LoadAvailableBeans();
                    
                    // Important: After loading beans, wait briefly to ensure UI is updated
                    await Task.Delay(100);

                    // Set time
                    TimeEntry.Text = $"{_roastToEdit.RoastMinutes:D2}:{_roastToEdit.RoastSeconds:D2}";
                    TimeDisplayLabel.Text = $"{_roastToEdit.RoastMinutes:D2}:{_roastToEdit.RoastSeconds:D2}";

                    // Set weights
                    BatchWeightEntry.Text = _roastToEdit.BatchWeight.ToString("F1");
                    FinalWeightEntry.Text = _roastToEdit.FinalWeight.ToString("F1");

                    // Set temperature
                    TemperatureEntry.Text = _roastToEdit.Temperature.ToString("F0");

                    // Set notes
                    NotesEditor.Text = _roastToEdit.Notes;
                    
                    // Set First Crack data if it was marked
                    if (_roastToEdit.FirstCrackSeconds.HasValue)
                    {
                        firstCrackMinutes = _roastToEdit.FirstCrackMinutes;
                        firstCrackSeconds = _roastToEdit.FirstCrackSeconds;
                        
                        FirstCrackLabel.Text = $"First Crack: {firstCrackMinutes:D2}:{firstCrackSeconds:D2}";
                        FirstCrackButton.IsEnabled = false;
                        FirstCrackButton.BackgroundColor = Colors.Gray;
                    }
                    else
                    {
                        ResetFirstCrackTracking();
                    }

                    // Calculate and update loss percentage and roast summary
                    double lossPercentage = _roastToEdit.WeightLossPercentage;
                    string roastLevel = await GetRoastLevel(lossPercentage);
                    LossPercentLabel.Text = $"Weight loss {lossPercentage:F1}% ({roastLevel} roast)";

                    // Now select the correct bean in the picker - make sure we have the bean type
                    string beanTypeToSelect = _roastToEdit.BeanType;
                    System.Diagnostics.Debug.WriteLine($"Now attempting to select bean: '{beanTypeToSelect}'");
                    if (!string.IsNullOrEmpty(beanTypeToSelect))
                    {
                        await SelectBeanInPicker(beanTypeToSelect);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Bean type from roast data is empty!");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in LoadRoastDataForEdit UI update: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading roast data for edit: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            await DisplayAlert("Error", "Failed to load roast data for editing", "OK");
        }
    }

    // Helper method to select the correct bean in the picker
    private async Task SelectBeanInPicker(string beanType)
    {
        try
        {
            if (string.IsNullOrEmpty(beanType))
            {
                System.Diagnostics.Debug.WriteLine("Bean type is empty, cannot select in picker");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"Trying to select bean: '{beanType}' in picker with {BeanPicker.Items.Count} items");
            System.Diagnostics.Debug.WriteLine($"Available beans count: {availableBeans.Count}");

            // Debug: Print all available items in picker for comparison
            for (int i = 0; i < BeanPicker.Items.Count; i++)
            {
                System.Diagnostics.Debug.WriteLine($"BeanPicker Item {i}: '{BeanPicker.Items[i]}'");
            }

            // Debug: Print all available beans objects 
            for (int i = 0; i < availableBeans.Count; i++)
            {
                System.Diagnostics.Debug.WriteLine($"AvailableBeans Item {i}: '{availableBeans[i].DisplayName}' (ID: {availableBeans[i].Id})");
            }

            // Disable the BeanPicker_SelectedIndexChanged event temporarily to avoid side effects
            BeanPicker.SelectedIndexChanged -= BeanPicker_SelectedIndexChanged;

            // First try exact match
            int beanIndex = -1;
            for (int i = 0; i < BeanPicker.Items.Count; i++)
            {
                if (string.Equals(BeanPicker.Items[i], beanType, StringComparison.Ordinal))
                {
                    beanIndex = i;
                    System.Diagnostics.Debug.WriteLine($"Found exact match for bean '{beanType}' at index {beanIndex}");
                    break;
                }
            }

            // If exact match failed, try case-insensitive match
            if (beanIndex == -1)
            {
                for (int i = 0; i < BeanPicker.Items.Count; i++)
                {
                    if (string.Equals(BeanPicker.Items[i], beanType, StringComparison.OrdinalIgnoreCase))
                    {
                        beanIndex = i;
                        System.Diagnostics.Debug.WriteLine($"Found case-insensitive match for bean '{beanType}' at index {beanIndex}");
                        break;
                    }
                }
            }

            // If we couldn't find a match, try to find a bean that contains the target name
            if (beanIndex == -1)
            {
                for (int i = 0; i < BeanPicker.Items.Count; i++)
                {
                    if (BeanPicker.Items[i].Contains(beanType, StringComparison.OrdinalIgnoreCase) ||
                        beanType.Contains(BeanPicker.Items[i], StringComparison.OrdinalIgnoreCase))
                    {
                        beanIndex = i;
                        System.Diagnostics.Debug.WriteLine($"Found partial match for bean '{beanType}' at index {beanIndex}: '{BeanPicker.Items[i]}'");
                        break;
                    }
                }
            }

            // If we found a match, select it and set the selectedBean
            if (beanIndex >= 0 && beanIndex < BeanPicker.Items.Count)
            {
                // Update UI on main thread since we're modifying UI elements
                await MainThread.InvokeOnMainThreadAsync(() => {
                    BeanPicker.SelectedIndex = beanIndex;

                    if (beanIndex < availableBeans.Count)
                    {
                        selectedBean = availableBeans[beanIndex];
                        System.Diagnostics.Debug.WriteLine($"Selected bean at index {beanIndex}: '{BeanPicker.Items[beanIndex]}' with ID: {selectedBean.Id}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Bean index {beanIndex} is out of range for availableBeans (Count: {availableBeans.Count})");
                    }
                });
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Could not find bean '{beanType}' in the picker items");
            }

            // Re-enable the event handler
            BeanPicker.SelectedIndexChanged += BeanPicker_SelectedIndexChanged;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error selecting bean in picker: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            // Use Task.Delay as another await point to satisfy the compiler warning
            await Task.Delay(1);
        }
    }

    // Cancel button handler: navigate based on edit mode
    private async void CancelButton_Clicked(object sender, EventArgs e)
    {
        if (_isEditMode)
            await Shell.Current.GoToAsync("//RoastLogPage");
        else
            await Shell.Current.GoToAsync("//MainPage");
    }

    // Method to prefill form fields from the previous roast data
    private void PrefillFieldsFromPreviousRoast()
    {
        try
        {
            // Only prefill if we're not in edit mode and we have previous roast data
            if (!_isEditMode && _previousRoast != null)
            {
                System.Diagnostics.Debug.WriteLine($"Previous roast data: ID={_previousRoast.Id}, BatchWeight={_previousRoast.BatchWeight}, Temp={_previousRoast.Temperature}");

                // Always prefill temperature field - bean selection happens first in workflow
                TemperatureEntry.Text = _previousRoast.Temperature.ToString("F0");
                System.Diagnostics.Debug.WriteLine($"Prefilled temperature: {TemperatureEntry.Text}");
                
                // Always prefill batch weight field - bean selection happens first in workflow
                var batchWeightValue = _previousRoast.BatchWeight;
                BatchWeightEntry.Text = batchWeightValue.ToString("F1");
                System.Diagnostics.Debug.WriteLine($"Prefilled batch weight: {BatchWeightEntry.Text} (from value: {batchWeightValue})");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error prefilling fields: {ex.Message}");
        }
    }
}
