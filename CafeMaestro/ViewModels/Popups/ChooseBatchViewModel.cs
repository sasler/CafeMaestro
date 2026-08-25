using System.Collections.ObjectModel;
using CafeMaestro.Models;
using CafeMaestro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafeMaestro.ViewModels.Popups;

public partial class ChooseBatchViewModel(IOverlayService overlayService) : ObservableObject, IQueryAttributable
{
    [ObservableProperty]
    public partial ObservableCollection<BatchChoice> Choices { get; set; } = [];

    [ObservableProperty]
    public partial BatchChoice? SelectedChoice { get; set; }

    public bool CanContinue => SelectedChoice is not null;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(nameof(Choices), out object? choices) &&
            choices is IReadOnlyList<BatchChoice> batchChoices)
        {
            Choices = new ObservableCollection<BatchChoice>(batchChoices);
        }
    }

    partial void OnSelectedChoiceChanged(BatchChoice? value)
    {
        OnPropertyChanged(nameof(CanContinue));
        ContinueCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanContinue))]
    private Task ContinueAsync() => overlayService.CloseAsync(new BatchChoiceOutcome(SelectedChoice));

    [RelayCommand]
    private Task CancelAsync() => overlayService.CloseAsync(BatchChoiceOutcome.Cancelled);
}
