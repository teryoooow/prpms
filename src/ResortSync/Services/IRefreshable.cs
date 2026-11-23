namespace ResortSync.Services;

/// <summary>
/// Pages that show queryable data implement this so background work (cloud
/// sync, auto-expiry) can reload the page that is currently on screen.
/// Pages with editable state (Settings, booking form) deliberately do NOT
/// implement it — a refresh must never discard the operator's input.
/// </summary>
public interface IRefreshable
{
    /// <summary>Reloads the page's data (called on the dispatcher thread).</summary>
    Task RefreshAsync();
}
