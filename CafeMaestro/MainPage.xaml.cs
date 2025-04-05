namespace CafeMaestro;

using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using System;
using System.Linq;

public partial class MainPage : ContentPage
{
    private TimerService timerService;
    private bool isTimerUpdating = false; // Flag to prevent recursive updates
    private string temporaryDigitsBuffer = ""; // Store digits before formatting

    public MainPage()
    {
        InitializeComponent();

        // Initialize TimerService
        timerService = new TimerService();
        timerService.TimeUpdated += OnTimeUpdated;

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
            TimeEntry.Text = $"{elapsedTime.Minutes:D2}:{elapsedTime.Seconds:D2}";
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
            
            // Don't auto-format yet - let user enter more digits first
            return;
        }
        else if (string.IsNullOrEmpty(text) || !text.Contains(':'))
        {
            // Keep the field empty while user is typing
            temporaryDigitsBuffer = "";
            return;
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
                    break;
                case 2: // Two digits = seconds (tens + units)
                    TimeEntry.Text = $"00:{temporaryDigitsBuffer}";
                    break;
                case 3: // Three digits = 1 minute + seconds
                    TimeEntry.Text = $"0{temporaryDigitsBuffer[0]}:{temporaryDigitsBuffer.Substring(1)}";
                    break;
                case 4: // Four digits = minutes + seconds
                    TimeEntry.Text = $"{temporaryDigitsBuffer.Substring(0, 2)}:{temporaryDigitsBuffer.Substring(2)}";
                    break;
                default: // More than 4 digits - take last 4
                    string lastFour = temporaryDigitsBuffer.Substring(temporaryDigitsBuffer.Length - 4);
                    TimeEntry.Text = $"{lastFour.Substring(0, 2)}:{lastFour.Substring(2)}";
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
}

