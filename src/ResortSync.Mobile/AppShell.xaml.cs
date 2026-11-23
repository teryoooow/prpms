namespace ResortSync.Mobile;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		Routing.RegisterRoute("booking/new", typeof(Views.NewBookingPage));
		Routing.RegisterRoute("booking/detail", typeof(Views.BookingDetailPage));
		Routing.RegisterRoute("payment/record", typeof(Views.RecordPaymentPage));
	}
}
