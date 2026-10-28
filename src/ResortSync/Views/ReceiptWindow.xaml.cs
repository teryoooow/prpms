using System.Windows;

namespace ResortSync.Views;

/// <summary>Receipt preview window with print / close actions.</summary>
public partial class ReceiptWindow : Window
{
    public ReceiptWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
