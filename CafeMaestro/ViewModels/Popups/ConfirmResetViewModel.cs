using CafeMaestro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafeMaestro.ViewModels.Popups;

public partial class ConfirmResetViewModel(IOverlayService overlayService) : ObservableObject, IQueryAttributable
{
    [ObservableProperty]
    public partial bool HasFirstCrack { get; set; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(nameof(HasFirstCrack), out object? value) && value is bool hasFirstCrack)
        {
            HasFirstCrack = hasFirstCrack;
        }
    }

    [RelayCommand]
    private Task ResetAsync() => overlayService.CloseAsync(true);

    [RelayCommand]
    private Task KeepAsync() => overlayService.CloseAsync(false);
}
