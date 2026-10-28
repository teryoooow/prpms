using System.Windows;
using ResortSync.ViewModels;

namespace ResortSync.Views;

/// <summary>
/// Application shell. All behavior lives in <see cref="MainViewModel"/>;
/// this class only performs construction and DataContext assignment.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
