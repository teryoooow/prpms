# Graph Report - Eden's Booking  (2026-10-27)

## Corpus Check
- 51 files · ~15,665 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 3 file(s) not represented in the graph (top: (none) 1, .iss 1, .ico 1)

## Summary
- 743 nodes · 1459 edges · 30 communities (25 shown, 5 thin omitted)
- Extraction: 83% EXTRACTED · 17% INFERRED · 0% AMBIGUOUS · INFERRED: 252 edges (avg confidence: 0.64)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Task
- .ConfigureServices
- PaymentsViewModel
- UserControl
- UserControl
- ResortSync.Services
- BookingFormViewModel
- UserControl
- MainViewModel
- PaymentTransaction
- ReceiptService
- Booking
- Type
- ResortSync.Views
- BookingExpiryService
- BookingsViewModel
- Window
- BookingAddon
- BookingService
- Guest
- ResortSync.csproj
- DashboardService
- List
- ResortDbContext
- IPaymentService
- AppConstants
- AGENTS.md

## God Nodes (most connected - your core abstractions)
1. `UserControl` - 78 edges
2. `BookingFormViewModel` - 72 edges
3. `UserControl` - 64 edges
4. `Booking` - 63 edges
5. `BookingsViewModel` - 52 edges
6. `PaymentsViewModel` - 36 edges
7. `UserControl` - 35 edges
8. `UserControl` - 34 edges
9. `DashboardViewModel` - 26 edges
10. `PaymentTransaction` - 24 edges

## Surprising Connections (you probably didn't know these)
- `UserControl` --references--> `AdditionalGuests`  [INFERRED]
  src/ResortSync/Views/BookingFormView.xaml → src/ResortSync/ViewModels/BookingFormViewModel.cs
- `UserControl` --references--> `AddonsTotal`  [INFERRED]
  src/ResortSync/Views/BookingFormView.xaml → src/ResortSync/ViewModels/BookingFormViewModel.cs
- `UserControl` --references--> `BaseAmount`  [INFERRED]
  src/ResortSync/Views/BookingFormView.xaml → src/ResortSync/ViewModels/BookingFormViewModel.cs
- `UserControl` --references--> `BaseGuests`  [INFERRED]
  src/ResortSync/Views/BookingFormView.xaml → src/ResortSync/ViewModels/BookingFormViewModel.cs
- `UserControl` --references--> `CancelCommand`  [INFERRED]
  src/ResortSync/Views/BookingFormView.xaml → src/ResortSync/ViewModels/BookingFormViewModel.cs

## Import Cycles
- None detected.

## Communities (30 total, 5 thin omitted)

### Community 0 - "Task"
Cohesion: 0.07
Nodes (11): BookingStatus, Cancelled, Completed, Confirmed, Expired, FullyPaid, PendingDeposit, BookingAddonInput (+3 more)

### Community 1 - ".ConfigureServices"
Cohesion: 0.05
Nodes (34): DefaultBaseGuests, DefaultExtraGuestRate, DownpaymentPercent, ResortName, Theme, Themes, DashboardInvalidator, IDashboardInvalidator (+26 more)

### Community 2 - "PaymentsViewModel"
Cohesion: 0.05
Nodes (39): BookingId, ExportCsvCommand, FromDate, GuestName, MethodFilters, OpenBookingCommand, Rows, SelectedItem (+31 more)

### Community 3 - "UserControl"
Cohesion: 0.04
Nodes (49): Bookings, CancelBookingCommand, InverseBoolToVis, StatusToBrush, DataContext.VoidPaymentCommand, Detail.CheckInDate, Detail.CheckOutDate, Detail.DepositDeadline (+41 more)

### Community 4 - "UserControl"
Cohesion: 0.04
Nodes (48): AdditionalGuests, Addons, AddonsTotal, BaseAmount, BaseGuests, CheckOutDate, ClearGuestCommand, ConflictMessage (+40 more)

### Community 5 - "ResortSync.Services"
Cohesion: 0.10
Nodes (8): ResortSync, ResortSync.Data, ResortSync.Constants, ResortSync.Models, ResortSync.ViewModels, ResortSync.Services, PricingCalculator, PricingResult

### Community 6 - "BookingFormViewModel"
Cohesion: 0.05
Nodes (37): BookingFormViewModel, AdditionalGuests, Addons, AddonsTotal, BaseAmount, BaseGuests, CancelCommand, CheckInDate (+29 more)

### Community 7 - "UserControl"
Cohesion: 0.06
Nodes (36): ActiveBookings, DeadlineLabel, DataContext.OpenAlertCommand, DepositAlerts, DepositDeadline, IsDepositExpiringSoon, IsDepositOverdue, MonthlyRevenue (+28 more)

### Community 8 - "MainViewModel"
Cohesion: 0.07
Nodes (29): CurrentViewModel, CurrentViewModel.Title, GoHomeCommand, Icon, NavItems, SelectedNavItem, BookingConflictException, Conflicts (+21 more)

### Community 9 - "PaymentTransaction"
Cohesion: 0.07
Nodes (26): PaymentMethod, BankTransfer, Cash, CreditCard, GCash, Maya, PaymentType, Downpayment (+18 more)

### Community 10 - "ReceiptService"
Cohesion: 0.12
Nodes (11): PaymentRow, IReceiptService, ReceiptService, AddonOption, AddonId, LineTotal, Name, PriceLabel (+3 more)

### Community 11 - "Booking"
Cohesion: 0.07
Nodes (27): Booking, AdditionalGuests, AddonsAmount, BaseAmount, BaseGuests, BookingAddons, CheckInDate, CheckOutDate (+19 more)

### Community 12 - "Type"
Cohesion: 0.16
Nodes (7): ResortSync.Converters, CurrencyConverter, DeadlineLabelConverter, InverseBoolConverter, InverseBoolToVisibilityConverter, IsEmptyToVisibilityConverter, StatusToBrushConverter

### Community 13 - "ResortSync.Views"
Cohesion: 0.12
Nodes (11): Document, PrintCommand, ResortSync.Views, BookingFormView, BookingsView, DashboardView, MainWindow, PaymentsView (+3 more)

### Community 14 - "BookingExpiryService"
Cohesion: 0.12
Nodes (5): Application, App, Services, DbInitializer, BookingExpiryService

### Community 15 - "BookingsViewModel"
Cohesion: 0.09
Nodes (20): BookingsViewModel, ApplyFiltersCommand, Bookings, CancelBookingCommand, Detail, DetailPaid, DetailPayments, EditBookingCommand (+12 more)

### Community 16 - "Window"
Cohesion: 0.12
Nodes (16): GuestSummary, Notes, OutstandingBalance, PaymentMethods, PaymentTypes, SetAmountCommand, SetHalfAmountCommand, ValidationMessage (+8 more)

### Community 17 - "BookingAddon"
Cohesion: 0.17
Nodes (9): BookingAddon, Addon, AddonId, Booking, BookingId, Id, LineTotal, Quantity (+1 more)

### Community 18 - "BookingService"
Cohesion: 0.30
Nodes (6): OvernightStayType, DayTour, NightTour, Overnight, TwentyTwoHourExclusive, BookingService

### Community 19 - "Guest"
Cohesion: 0.18
Nodes (10): Guest, Bookings, DisplayName, Email, FullName, Id, IdNumber, IdType (+2 more)

### Community 20 - "ResortSync.csproj"
Cohesion: 0.22
Nodes (7): net8.0-windows, CommunityToolkit.Mvvm (8.2.2), MaterialDesignThemes (5.1.0), Microsoft.EntityFrameworkCore.Sqlite (8.0.8), Microsoft.Extensions.Hosting (8.0.0), Microsoft.Xaml.Behaviors.Wpf (1.1.77), Microsoft.NET.Sdk

### Community 21 - "DashboardService"
Cohesion: 0.33
Nodes (3): DashboardService, DashboardStats, IDashboardService

### Community 22 - "List"
Cohesion: 0.31
Nodes (6): Addon, BookingAddons, Id, IsActive, Name, UnitPrice

### Community 23 - "ResortDbContext"
Cohesion: 0.25
Nodes (6): ResortDbContext, Addons, BookingAddons, Bookings, Guests, PaymentTransactions

### Community 25 - "AppConstants"
Cohesion: 0.33
Nodes (5): AppConstants, ConnectionString, DatabasePath, DataDirectory, SettingsPath

## Knowledge Gaps
- **315 isolated node(s):** `Services`, `DataDirectory`, `DatabasePath`, `ConnectionString`, `SettingsPath` (+310 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 359 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **5 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `BookingFormViewModel` connect `BookingFormViewModel` to `Task`, `.ConfigureServices`, `PaymentsViewModel`, `UserControl`, `ResortSync.Services`, `UserControl`, `MainViewModel`, `ReceiptService`, `Booking`, `BookingService`, `Guest`, `.ValidateDatesAsync`, `.Recalculate`?**
  _High betweenness centrality (0.250) - this node is a cross-community bridge._
- **Are the 29 inferred relationships involving `UserControl` (e.g. with `BookingFormViewModel` and `AdditionalGuests`) actually correct?**
  _`UserControl` has 29 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Services`, `DataDirectory`, `DatabasePath` to the rest of the system?**
  _315 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Task` be split into smaller, more focused modules?**
  _Cohesion score 0.07039337474120083 - nodes in this community are weakly interconnected._
- **Why does `BookingsViewModel` connect `BookingsViewModel` to `Task`, `.ConfigureServices`, `UserControl`, `ResortSync.Services`, `UserControl`, `MainViewModel`, `PaymentTransaction`, `Booking`, `IPaymentService`?**
  _High betweenness centrality (0.193) - this node is a cross-community bridge._
- **Are the 32 inferred relationships involving `BookingFormViewModel` (e.g. with `AdditionalGuests` and `AddonsTotal`) actually correct?**
  _`BookingFormViewModel` has 32 INFERRED edges - model-reasoned connections that need verification._
- **Should `.ConfigureServices` be split into smaller, more focused modules?**
  _Cohesion score 0.052597402597402594 - nodes in this community are weakly interconnected._