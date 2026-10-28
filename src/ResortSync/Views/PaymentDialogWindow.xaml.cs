using System.Windows;
using ResortSync.ViewModels;

namespace ResortSync.Views;

/// <summary>
/// Modal payment entry window. The ViewModel signals completion through
/// <see cref="PaymentDialogViewModel.RequestClose"/>; this class maps it to DialogResult.
/// </summary>
public partial class PaymentDialogWindow : Window
{
    private PaymentDialogViewModel? _subscribed;

    public PaymentDialogWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // Unsubscribe from the previous VM if the DataContext is ever swapped.
        if (_subscribed is not null)
        {
            _subscribed.RequestClose -= OnRequestClose;
            _subscribed = null;
        }

        if (e.NewValue is PaymentDialogViewModel viewModel)
        {
            viewModel.RequestClose += OnRequestClose;
            _subscribed = viewModel;
        }
    }

    private void OnRequestClose(object? sender, bool result)
    {
        // DialogResult can only be set while the window is shown as a dialog.
        if (IsVisible)
        {
            DialogResult = result;
            Close();
        }
    }
}
