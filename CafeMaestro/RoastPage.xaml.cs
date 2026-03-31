using System.ComponentModel;
using System.Diagnostics;
using CafeMaestro.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace CafeMaestro;

public partial class RoastPage : ContentPage, IQueryAttributable
{
    private readonly RoastPageViewModel _viewModel;
    private bool isTimerUpdating;
    private string temporaryDigitsBuffer = string.Empty;
    private CancellationTokenSource? _animationCancellationTokenSource;
    private CancellationTokenSource? _cursorAnimationCancellationTokenSource;

    public RoastPage(RoastPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _viewModel.ApplyQueryAttributes(query);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.OnAppearingAsync();
        SyncTimeEntry();
        UpdateFirstCrackButtonVisual();
    }

    protected override void OnDisappearing()
    {
        StopTimerPulseAnimation();
        StopTimerEditAnimation();
        _viewModel.OnDisappearing();
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        MainThread.BeginInvokeOnMainThread(async () => await _viewModel.HandleBackNavigationAsync());
        return true;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RoastPageViewModel.TimerDisplay))
        {
            SyncTimeEntry();
        }
        else if (e.PropertyName == nameof(RoastPageViewModel.IsTimerRunning))
        {
            if (_viewModel.IsTimerRunning)
            {
                StartTimerPulseAnimation();
            }
            else
            {
                StopTimerPulseAnimation();
            }
        }
        else if (e.PropertyName == nameof(RoastPageViewModel.CanMarkFirstCrack) ||
                 e.PropertyName == nameof(RoastPageViewModel.FirstCrackSeconds))
        {
            UpdateFirstCrackButtonVisual();
        }
    }

    private void SyncTimeEntry()
    {
        if (TimeEntry.IsFocused)
        {
            return;
        }

        isTimerUpdating = true;
        TimeEntry.Text = _viewModel.TimerDisplay;
        isTimerUpdating = false;
    }

    private void TimeEntry_Focused(object sender, FocusEventArgs e)
    {
        if (sender is not Entry entry || !e.IsFocused)
        {
            return;
        }

        isTimerUpdating = true;
        entry.Text = string.Empty;
        isTimerUpdating = false;

        StartTimerEditAnimation();
        entry.Completed += TimeEntry_Completed;
    }

    private void TimeEntry_Completed(object? sender, EventArgs e)
    {
        if (sender is not Entry entry)
        {
            return;
        }

        entry.Unfocus();
        entry.Completed -= TimeEntry_Completed;
        StopTimerEditAnimation();
    }

    private void TimeEntry_Unfocused(object sender, FocusEventArgs e)
    {
        if (isTimerUpdating)
        {
            return;
        }

        StopTimerEditAnimation();

        string formattedTime = string.IsNullOrEmpty(temporaryDigitsBuffer)
            ? "00:00"
            : FormatDigitsAsTime(temporaryDigitsBuffer);

        if (formattedTime.Contains(':'))
        {
            string[] parts = formattedTime.Split(':');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out int minutes) &&
                int.TryParse(parts[1], out int seconds))
            {
                if (seconds >= 60)
                {
                    minutes += seconds / 60;
                    seconds %= 60;
                }

                formattedTime = $"{minutes:D2}:{seconds:D2}";
            }
        }

        temporaryDigitsBuffer = string.Empty;

        isTimerUpdating = true;
        TimeEntry.Text = formattedTime;
        isTimerUpdating = false;
        _viewModel.SetManualTimerDisplay(formattedTime);
    }

    private void TimeEntry_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (isTimerUpdating)
        {
            return;
        }

        string text = e.NewTextValue ?? string.Empty;
        string inputDigits = new(text.Where(char.IsDigit).ToArray());

        if (!string.IsNullOrEmpty(inputDigits))
        {
            temporaryDigitsBuffer = inputDigits;
            _viewModel.SetManualTimerDisplay(FormatDigitsAsTime(temporaryDigitsBuffer));
            return;
        }

        if (string.IsNullOrEmpty(text) || !text.Contains(':'))
        {
            temporaryDigitsBuffer = string.Empty;
        }
    }

    private static string FormatDigitsAsTime(string digits)
    {
        if (string.IsNullOrEmpty(digits))
        {
            return "00:00";
        }

        return digits.Length switch
        {
            1 => $"00:0{digits}",
            2 => $"00:{digits}",
            3 => $"0{digits[0]}:{digits[1..]}",
            4 => $"{digits[..2]}:{digits[2..]}",
            _ => $"{digits[^4..^2]}:{digits[^2..]}"
        };
    }

    private void StartTimerPulseAnimation()
    {
        TimerRunningIndicator.IsVisible = true;

        _animationCancellationTokenSource?.Cancel();
        _animationCancellationTokenSource = new CancellationTokenSource();

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                CancellationToken token = _animationCancellationTokenSource.Token;

                while (!token.IsCancellationRequested)
                {
                    TimerRunningIndicator.IsVisible = true;
                    await Task.Delay(500, token);

                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    TimerRunningIndicator.IsVisible = false;
                    await Task.Delay(500, token);
                }
            }
            catch (TaskCanceledException ex)
            {
                Debug.WriteLine($"Timer pulse animation canceled: {ex.Message}");
            }
        });
    }

    private void StopTimerPulseAnimation()
    {
        _animationCancellationTokenSource?.Cancel();
        _animationCancellationTokenSource = null;
        TimerRunningIndicator.IsVisible = false;
    }

    private void StartTimerEditAnimation()
    {
        _cursorAnimationCancellationTokenSource?.Cancel();
        _cursorAnimationCancellationTokenSource = new CancellationTokenSource();

        Microsoft.Maui.Graphics.Color originalColor = TimeDisplayLabel.TextColor;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                CancellationToken token = _cursorAnimationCancellationTokenSource.Token;

                while (!token.IsCancellationRequested)
                {
                    TimeDisplayLabel.TextColor = originalColor;
                    await Task.Delay(500, token);

                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    TimeDisplayLabel.TextColor = originalColor.WithAlpha(0.3f);
                    await Task.Delay(500, token);
                }
            }
            catch (TaskCanceledException ex)
            {
                Debug.WriteLine($"Timer edit animation canceled: {ex.Message}");
            }
            finally
            {
                TimeDisplayLabel.TextColor = originalColor;
                TimeDisplayLabel.Opacity = 1.0;
            }
        });
    }

    private void StopTimerEditAnimation()
    {
        try
        {
            _cursorAnimationCancellationTokenSource?.Cancel();
            _cursorAnimationCancellationTokenSource?.Dispose();
            _cursorAnimationCancellationTokenSource = null;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (Application.Current?.Resources != null &&
                    Application.Current.Resources.TryGetValue("PrimaryColor", out object? colorObj) &&
                    colorObj is Microsoft.Maui.Graphics.Color color)
                {
                    TimeDisplayLabel.TextColor = color;
                }

                TimeDisplayLabel.Opacity = 1.0;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to stop timer edit animation cleanly: {ex.Message}");
            TimeDisplayLabel.Opacity = 1.0;
        }
    }

    private void UpdateFirstCrackButtonVisual()
    {
        if (Application.Current?.Resources is null ||
            !Application.Current.Resources.TryGetValue("AccentColor", out object? colorObj) ||
            colorObj is not Microsoft.Maui.Graphics.Color accentColor)
        {
            return;
        }

        FirstCrackButton.BackgroundColor = _viewModel.FirstCrackSeconds.HasValue ? Colors.Gray : accentColor;
    }
}
