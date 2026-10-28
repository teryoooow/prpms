# Graph Report - Eden's Booking  (2026-10-28)

## Corpus Check
- 52 files · ~16,654 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 3 file(s) not represented in the graph (top: (none) 1, .iss 1, .ico 1)

## Summary
- 755 nodes · 1477 edges · 36 communities (30 shown, 6 thin omitted)
- Extraction: 82% EXTRACTED · 18% INFERRED · 0% AMBIGUOUS · INFERRED: 259 edges (avg confidence: 0.65)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `bf600e7c`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Task
- .ConfigureServices
- UserControl
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
- PaymentsViewModel
- .ConfigureServices
- BookingsViewModel
- Window
- BookingAddon
- BookingService
- Guest
- ResortSync.csproj
- PRPMS — Private Resort Payment Monitoring System
- List
- ResortDbContext
- IPaymentService
- AppConstants
- AGENTS.md
- PaymentDialogViewModel
- PaymentType
- PaymentMethod
- PaymentRow
- PaymentDialogWindow
- PaymentsGrid

## God Nodes (most connected - your core abstractions)
1. `UserControl` - 78 edges
2. `BookingFormViewModel` - 72 edges
3. `Booking` - 64 edges
4. `UserControl` - 64 edges
5. `BookingsViewModel` - 52 edges
6. `PaymentsViewModel` - 36 edges
7. `UserControl` - 35 edges
8. `UserControl` - 34 edges
9. `DashboardViewModel` - 26 edges
10. `PaymentTransaction` - 25 edges

## Surprising Connections (you probably didn't know these)
- `How This App Was Built` --references--> `Addon`  [INFERRED]
  README.md → src/ResortSync/Models/Addon.cs
- `How This App Was Built` --references--> `Booking`  [INFERRED]
  README.md → src/ResortSync/Models/Booking.cs
- `How This App Was Built` --references--> `BookingAddon`  [INFERRED]
  README.md → src/ResortSync/Models/BookingAddon.cs
- `How This App Was Built` --references--> `Guest`  [INFERRED]
  README.md → src/ResortSync/Models/Guest.cs
- `How This App Was Built` --references--> `PaymentTransaction`  [INFERRED]
  README.md → src/ResortSync/Models/PaymentTransaction.cs

## Import Cycles
- None detected.

## Communities (36 total, 6 thin omitted)

### Community 0 - "Task"
Cohesion: 0.06
Nodes (17): BookingStatus, Cancelled, Completed, Confirmed, Expired, FullyPaid, PendingDeposit, BookingAddonInput (+9 more)

### Community 1 - ".ConfigureServices"
Cohesion: 0.08
Nodes (24): DefaultBaseGuests, DefaultExtraGuestRate, DownpaymentPercent, ResortName, Theme, Themes, INavigationAware, IThemeService (+16 more)

### Community 2 - "UserControl"
Cohesion: 0.09
Nodes (22): BookingId, ExportCsvCommand, FromDate, GuestName, MethodFilters, OpenBookingCommand, SelectedItem, SelectedMethodFilter (+14 more)

### Community 3 - "UserControl"
Cohesion: 0.04
Nodes (49): Bookings, CancelBookingCommand, InverseBoolToVis, StatusToBrush, DataContext.VoidPaymentCommand, Detail.CheckInDate, Detail.CheckOutDate, Detail.DepositDeadline (+41 more)

### Community 4 - "UserControl"
Cohesion: 0.04
Nodes (48): AdditionalGuests, Addons, AddonsTotal, BaseAmount, BaseGuests, CheckOutDate, ClearGuestCommand, ConflictMessage (+40 more)

### Community 5 - "ResortSync.Services"
Cohesion: 0.07
Nodes (15): ResortSync.Views, ResortSync, ResortSync.Data, ResortSync.Constants, ResortSync.Models, ResortSync.ViewModels, ResortSync.Services, PricingCalculator (+7 more)

### Community 6 - "BookingFormViewModel"
Cohesion: 0.05
Nodes (37): BookingFormViewModel, AdditionalGuests, Addons, AddonsTotal, BaseAmount, BaseGuests, CancelCommand, CheckInDate (+29 more)

### Community 7 - "UserControl"
Cohesion: 0.06
Nodes (39): ActiveBookings, DeadlineLabel, DataContext.OpenAlertCommand, DepositAlerts, DepositDeadline, IsDepositExpiringSoon, IsDepositOverdue, MonthlyRevenue (+31 more)

### Community 8 - "MainViewModel"
Cohesion: 0.07
Nodes (29): CurrentViewModel, CurrentViewModel.Title, GoHomeCommand, Icon, NavItems, SelectedNavItem, BookingConflictException, Conflicts (+21 more)

### Community 9 - "PaymentTransaction"
Cohesion: 0.18
Nodes (10): PaymentTransaction, AmountPaid, Booking, BookingId, Id, Notes, PaymentDate, PaymentMethod (+2 more)

### Community 10 - "ReceiptService"
Cohesion: 0.14
Nodes (10): IReceiptService, ReceiptService, AddonOption, AddonId, LineTotal, Name, PriceLabel, UnitPrice (+2 more)

### Community 11 - "Booking"
Cohesion: 0.08
Nodes (26): Booking, AdditionalGuests, AddonsAmount, BaseAmount, BaseGuests, BookingAddons, CheckInDate, CheckOutDate (+18 more)

### Community 12 - "Type"
Cohesion: 0.15
Nodes (7): ResortSync.Converters, CurrencyConverter, DeadlineLabelConverter, InverseBoolConverter, InverseBoolToVisibilityConverter, IsEmptyToVisibilityConverter, StatusToBrushConverter

### Community 13 - "PaymentsViewModel"
Cohesion: 0.14
Nodes (13): PaymentsViewModel, ExportCsvCommand, FromDate, MethodFilters, OpenBookingCommand, RefreshCommand, Rows, SearchText (+5 more)

### Community 14 - ".ConfigureServices"
Cohesion: 0.05
Nodes (15): Document, PrintCommand, Architecture, Application, App, Services, DbInitializer, BookingExpiryService (+7 more)

### Community 15 - "BookingsViewModel"
Cohesion: 0.09
Nodes (20): BookingsViewModel, ApplyFiltersCommand, Bookings, CancelBookingCommand, Detail, DetailPaid, DetailPayments, EditBookingCommand (+12 more)

### Community 16 - "Window"
Cohesion: 0.12
Nodes (16): GuestSummary, Notes, OutstandingBalance, PaymentMethods, PaymentTypes, SetAmountCommand, SetHalfAmountCommand, ValidationMessage (+8 more)

### Community 17 - "BookingAddon"
Cohesion: 0.20
Nodes (9): BookingAddon, Addon, AddonId, Booking, BookingId, Id, LineTotal, Quantity (+1 more)

### Community 18 - "BookingService"
Cohesion: 0.31
Nodes (6): OvernightStayType, DayTour, NightTour, Overnight, TwentyTwoHourExclusive, BookingService

### Community 19 - "Guest"
Cohesion: 0.18
Nodes (10): Guest, Bookings, DisplayName, Email, FullName, Id, IdNumber, IdType (+2 more)

### Community 20 - "ResortSync.csproj"
Cohesion: 0.22
Nodes (7): net8.0-windows, CommunityToolkit.Mvvm (8.2.2), MaterialDesignThemes (5.1.0), Microsoft.EntityFrameworkCore.Sqlite (8.0.8), Microsoft.Extensions.Hosting (8.0.0), Microsoft.Xaml.Behaviors.Wpf (1.1.77), Microsoft.NET.Sdk

### Community 21 - "PRPMS — Private Resort Payment Monitoring System"
Cohesion: 0.18
Nodes (10): Credits, Data Locations, Developers, End users, Features, Getting the App, How This App Was Built, License (+2 more)

### Community 22 - "List"
Cohesion: 0.29
Nodes (6): Addon, BookingAddons, Id, IsActive, Name, UnitPrice

### Community 23 - "ResortDbContext"
Cohesion: 0.20
Nodes (6): ResortDbContext, Addons, BookingAddons, Bookings, Guests, PaymentTransactions

### Community 24 - "IPaymentService"
Cohesion: 0.29
Nodes (3): IPaymentService, PaymentFilter, PaymentService

### Community 25 - "AppConstants"
Cohesion: 0.33
Nodes (5): AppConstants, ConnectionString, DatabasePath, DataDirectory, SettingsPath

### Community 30 - "PaymentDialogViewModel"
Cohesion: 0.25
Nodes (4): PaymentDialogViewModel, PaymentMethods, PaymentTypes, Result

### Community 31 - "PaymentType"
Cohesion: 0.33
Nodes (5): PaymentType, Downpayment, FinalBalance, Refund, SecurityDeposit

### Community 32 - "PaymentMethod"
Cohesion: 0.33
Nodes (6): PaymentMethod, BankTransfer, Cash, CreditCard, GCash, Maya

### Community 35 - "PaymentsGrid"
Cohesion: 0.67
Nodes (3): Rows, PaymentsGrid, DataGrid

## Knowledge Gaps
- **315 isolated node(s):** `Services`, `DataDirectory`, `DatabasePath`, `ConnectionString`, `SettingsPath` (+310 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 367 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **6 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `BookingFormViewModel` connect `BookingFormViewModel` to `Task`, `.ConfigureServices`, `UserControl`, `ResortSync.Services`, `MainViewModel`, `ReceiptService`, `Booking`, `PaymentsViewModel`, `.ConfigureServices`, `BookingService`, `Guest`, `.ValidateDatesAsync`, `.Recalculate`?**
  _High betweenness centrality (0.247) - this node is a cross-community bridge._
- **Are the 29 inferred relationships involving `UserControl` (e.g. with `BookingFormViewModel` and `AdditionalGuests`) actually correct?**
  _`UserControl` has 29 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Services`, `DataDirectory`, `DatabasePath` to the rest of the system?**
  _315 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Task` be split into smaller, more focused modules?**
  _Cohesion score 0.058115025594700394 - nodes in this community are weakly interconnected._
- **Why does `BookingsViewModel` connect `BookingsViewModel` to `Task`, `.ConfigureServices`, `UserControl`, `ResortSync.Services`, `MainViewModel`, `PaymentTransaction`, `Booking`, `.ConfigureServices`, `IPaymentService`?**
  _High betweenness centrality (0.191) - this node is a cross-community bridge._
- **Are the 32 inferred relationships involving `BookingFormViewModel` (e.g. with `AdditionalGuests` and `AddonsTotal`) actually correct?**
  _`BookingFormViewModel` has 32 INFERRED edges - model-reasoned connections that need verification._
- **Should `.ConfigureServices` be split into smaller, more focused modules?**
  _Cohesion score 0.08266129032258064 - nodes in this community are weakly interconnected._