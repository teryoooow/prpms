namespace ResortSync.Services;

/// <summary>
/// Implemented by ViewModels that need to load data every time they are navigated to.
/// </summary>
public interface INavigationAware
{
    /// <summary>
    /// Called after the ViewModel becomes the active view.
    /// </summary>
    /// <param name="parameter">
    /// Optional navigation argument (e.g. a booking Id when opening the form in edit mode).
    /// </param>
    Task OnNavigatedToAsync(object? parameter = null);
}
