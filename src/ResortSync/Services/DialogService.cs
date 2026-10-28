using System.Windows;
using ResortSync.Models;
using ResortSync.ViewModels;
using ResortSync.Views;

namespace ResortSync.Services;

/// <summary>
/// Centralized access to modal windows and message boxes so ViewModels never
/// touch window code-behind directly.
/// </summary>
public interface IDialogService
{
    void ShowInfo(string title, string message);
    void ShowError(string title, string message);
    bool Confirm(string title, string message);

    /// <summary>Opens the record-payment modal; null when cancelled.</summary>
    PaymentTransaction? ShowRecordPaymentDialog(int bookingId, decimal suggestedAmount);

    /// <summary>Opens the printable receipt window for a booking.</summary>
    void ShowReceipt(Booking booking);

    /// <summary>Save-file picker; null when cancelled.</summary>
    string? PickSaveFilePath(string filter, string defaultFileName);
}

public sealed class DialogService : IDialogService
{
    private readonly IServiceProvider _services;

    public DialogService(IServiceProvider services) => _services = services;

    public void ShowInfo(string title, string message)
        => MessageBox.Show(Application.Current.MainWindow!, message, title,
            MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowError(string title, string message)
        => MessageBox.Show(Application.Current.MainWindow!, message, title,
            MessageBoxButton.OK, MessageBoxImage.Error);

    public bool Confirm(string title, string message)
        => MessageBox.Show(Application.Current.MainWindow!, message, title,
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public PaymentTransaction? ShowRecordPaymentDialog(int bookingId, decimal suggestedAmount)
    {
        var viewModel = new PaymentDialogViewModel(_services, bookingId, suggestedAmount);
        var window = new PaymentDialogWindow
        {
            Owner = Application.Current.MainWindow,
            DataContext = viewModel
        };

        return window.ShowDialog() == true ? viewModel.Result : null;
    }

    public void ShowReceipt(Booking booking)
    {
        var receiptService = (IReceiptService)_services.GetService(typeof(IReceiptService))!;
        var settingsService = (ISettingsService)_services.GetService(typeof(ISettingsService))!;
        var settings = settingsService.LoadAsync().GetAwaiter().GetResult();

        var receiptViewModel = new ReceiptViewModel(
            receiptService.BuildReceipt(booking, settings.ResortName), receiptService);

        var window = new ReceiptWindow
        {
            Owner = Application.Current.MainWindow,
            DataContext = receiptViewModel
        };
        window.Show();
    }

    public string? PickSaveFilePath(string filter, string defaultFileName)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = filter,
            FileName = defaultFileName
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
