using System.Collections.ObjectModel;
using System.Globalization;
using CafeMaestro.Models;
using CafeMaestro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafeMaestro.ViewModels.Popups;

/// <summary>
/// One selectable row. Batches of the same bean are told apart by batch number, drop time, and
/// input weight, and selection is shown as a word as well as a highlighted card.
/// </summary>
public partial class BatchChoiceOption : ObservableObject
{
    public required BatchChoice Choice { get; init; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public string BatchDisplay => Choice.BatchNumber is int number ? $"B{number}" : "—";
    public string BeanDisplay => Choice.BeanDisplaySnapshot;
    public string DetailDisplay =>
        $"{Choice.BatchWeight:0.0} g in · dropped {Choice.DroppedAtUtc.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture)}";
    public string SelectionDisplay => IsSelected ? "SELECTED" : "TAP TO SELECT";

    public string SemanticDescription =>
        $"{(Choice.BatchNumber is int number ? $"Batch {number}" : "Batch")}, {BeanDisplay}, {DetailDisplay}. {SelectionDisplay}.";

    partial void OnIsSelectedChanged(bool value) => OnPropertyChanged(nameof(SelectionDisplay));
}

public partial class ChooseBatchViewModel(IOverlayService overlayService) : ObservableObject, IQueryAttributable
{
    [ObservableProperty]
    public partial ObservableCollection<BatchChoiceOption> Options { get; set; } = [];

    [ObservableProperty]
    public partial BatchChoice? SelectedChoice { get; set; }

    public bool CanContinue => SelectedChoice is not null;

    /// <summary>The parameter key the overlay service passes the batches under.</summary>
    public const string ChoicesKey = "Choices";

    public void SetChoices(IReadOnlyList<BatchChoice> choices)
    {
        Options = new ObservableCollection<BatchChoiceOption>(
            choices.Select(choice => new BatchChoiceOption { Choice = choice }));
        SelectedChoice = null;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(ChoicesKey, out object? value) && value is IReadOnlyList<BatchChoice> choices)
        {
            SetChoices(choices);
        }
    }

    partial void OnSelectedChoiceChanged(BatchChoice? value)
    {
        OnPropertyChanged(nameof(CanContinue));
        ContinueCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void Select(BatchChoiceOption? option)
    {
        if (option is null)
        {
            return;
        }

        foreach (BatchChoiceOption candidate in Options)
        {
            candidate.IsSelected = ReferenceEquals(candidate, option);
        }

        SelectedChoice = option.Choice;
    }

    [RelayCommand(CanExecute = nameof(CanContinue))]
    private Task ContinueAsync() => overlayService.CloseAsync(new BatchChoiceOutcome(SelectedChoice));

    [RelayCommand]
    private Task CancelAsync() => overlayService.CloseAsync(BatchChoiceOutcome.Cancelled);
}
