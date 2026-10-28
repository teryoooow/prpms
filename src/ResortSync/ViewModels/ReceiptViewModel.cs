using System.Windows.Documents;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResortSync.Services;

namespace ResortSync.ViewModels;

/// <summary>Hosts the generated receipt FlowDocument with print / close actions.</summary>
public partial class ReceiptViewModel : ObservableObject
{
    private readonly IReceiptService _receiptService;

    public FlowDocument Document { get; }

    public ReceiptViewModel(FlowDocument document, IReceiptService receiptService)
    {
        Document = document;
        _receiptService = receiptService;
    }

    [RelayCommand]
    private void Print() => _receiptService.Print(Document);
}
