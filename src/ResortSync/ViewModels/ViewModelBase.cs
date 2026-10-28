using CommunityToolkit.Mvvm.ComponentModel;
using ResortSync.Services;

namespace ResortSync.ViewModels;

/// <summary>
/// Base class for all ViewModels: busy indicator + navigation lifecycle hook.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    /// <summary>Drives progress spinners and disables command buttons while work runs.</summary>
    [ObservableProperty]
    private bool isBusy;

    /// <summary>Page title shown in the app bar.</summary>
    [ObservableProperty]
    private string title = string.Empty;

    /// <summary>Optional status line under the page title.</summary>
    [ObservableProperty]
    private string statusMessage = string.Empty;

    /// <summary>Default no-op so every ViewModel can be navigated to uniformly.</summary>
    public virtual Task OnNavigatedToAsync(object? parameter = null) => Task.CompletedTask;

    /// <summary>Runs an async operation with the busy flag managed automatically.</summary>
    protected async Task RunBusyAsync(Func<Task> operation)
    {
        if (IsBusy)
        {
            return; // prevent re-entrancy from double-clicks
        }

        try
        {
            IsBusy = true;
            await operation();
        }
        finally
        {
            IsBusy = false;
        }
    }
}
