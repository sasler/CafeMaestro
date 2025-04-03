namespace CafeMaestro;

using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel; // Added for MainThread access
using System;

public partial class MainPage : ContentPage
{
    private TimerService timerService;

    public MainPage()
    {
        InitializeComponent();

        // Initialize TimerService
        timerService = new TimerService();
        timerService.TimeUpdated += OnTimeUpdated;

        // Set initial timer visibility
        UpdateTimerVisibility();

        // Attach event handlers for weight entry text changes
        BatchWeightEntry.TextChanged += OnWeightEntryTextChanged;
        FinalWeightEntry.TextChanged += OnWeightEntryTextChanged;
    }

    private void OnCalculateClicked(object sender, EventArgs e)
    {
        try
        {
            // Validate and parse input values
            if (!ValidateInputs(out double batchWeight, out double finalWeight, out double temperature, 
                out int roastMinutes, out int roastSeconds))
            {
                return;
            }

            // Convert roast time to a formatted string (for display)
            string formattedRoastTime = FormatRoastTime(roastMinutes, roastSeconds);
            
            // Calculate roast time in minutes (for calculations)
            double roastTimeInMinutes = roastMinutes + (roastSeconds / 60.0);

            // Calculate loss percentage
            double lossWeight = batchWeight - finalWeight;
            double lossPercentage = (lossWeight / batchWeight) * 100;

            // Update the UI with calculated values
            LossPercentLabel.Text = $"Weight Loss: {lossPercentage:F2}%";
            
            // Generate roast summary based on the loss percentage
            string roastLevel = GetRoastLevel(lossPercentage);
            RoastSummaryLabel.Text = $"Roast Summary: {roastLevel} roast at {temperature}°C for {formattedRoastTime}";
        }
        catch (Exception ex)
        {
            DisplayAlert("Calculation Error", $"An error occurred: {ex.Message}", "OK");
        }
    }

    private bool ValidateInputs(out double batchWeight, out double finalWeight, out double temperature, 
        out int roastMinutes, out int roastSeconds)
    {
        batchWeight = 0;
        finalWeight = 0;
        temperature = 0;
        roastMinutes = 0;
        roastSeconds = 0;

        // Validate batch weight
        if (string.IsNullOrWhiteSpace(BatchWeightEntry.Text) ||
            !double.TryParse(BatchWeightEntry.Text, out batchWeight) ||
            batchWeight <= 0)
        {
            DisplayAlert("Validation Error", "Please enter a valid batch weight in grams.", "OK");
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

        // Validate roasting minutes
        if (string.IsNullOrWhiteSpace(RoastMinutesEntry.Text) ||
            !int.TryParse(RoastMinutesEntry.Text, out roastMinutes) ||
            roastMinutes < 0)
        {
            DisplayAlert("Validation Error", "Please enter valid minutes (0 or more).", "OK");
            return false;
        }

        // Validate roasting seconds
        if (string.IsNullOrWhiteSpace(RoastSecondsEntry.Text) ||
            !int.TryParse(RoastSecondsEntry.Text, out roastSeconds) ||
            roastSeconds < 0 || roastSeconds > 59)
        {
            DisplayAlert("Validation Error", "Please enter valid seconds (0-59).", "OK");
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
        TimerDisplay.Text = $"{elapsedTime.Minutes:D2}:{elapsedTime.Seconds:D2}";
    }

    private void StartTimer_Clicked(object sender, EventArgs e)
    {
        timerService.Start();
        StartTimerButton.IsEnabled = false;
        PauseTimerButton.IsEnabled = true;
        StopTimerButton.IsEnabled = true;
    }

    private void PauseTimer_Clicked(object sender, EventArgs e)
    {
        timerService.Stop();
        StartTimerButton.IsEnabled = true;
        PauseTimerButton.IsEnabled = false;
    }

    private void StopTimer_Clicked(object sender, EventArgs e)
    {
        timerService.Stop();
        TimeSpan elapsedTime = timerService.GetElapsedTime();

        // Populate the roasting time fields
        RoastMinutesEntry.Text = elapsedTime.Minutes.ToString();
        RoastSecondsEntry.Text = elapsedTime.Seconds.ToString();

        // Switch back to manual entry view
        ManualEntryRadioButton.IsChecked = true;

        // Reset button states
        StartTimerButton.IsEnabled = true;
        PauseTimerButton.IsEnabled = false;
        StopTimerButton.IsEnabled = false;
    }

    private void ResetTimer_Clicked(object sender, EventArgs e)
    {
        timerService.Reset();
        StartTimerButton.IsEnabled = true;
        PauseTimerButton.IsEnabled = false;
        StopTimerButton.IsEnabled = false;
    }

    private void UseTimerValue_Clicked(object sender, EventArgs e)
    {
        timerService.Stop();
        TimeSpan elapsedTime = timerService.GetElapsedTime();

        // Populate the roasting time fields
        RoastMinutesEntry.Text = elapsedTime.Minutes.ToString();
        RoastSecondsEntry.Text = elapsedTime.Seconds.ToString();

        // Switch back to manual entry view
        ManualEntryRadioButton.IsChecked = true;
    }

    private void TimeEntryMethod_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        UpdateTimerVisibility();
    }

    private void UpdateTimerVisibility()
    {
        bool useTimer = UseTimerRadioButton.IsChecked;

        TimerContainer.IsVisible = useTimer;
        ManualTimeEntryContainer.IsVisible = !useTimer;
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
}

