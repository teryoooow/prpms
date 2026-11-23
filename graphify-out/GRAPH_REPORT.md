# Graph Report - Eden's Booking  (2026-11-23)

## Corpus Check
- 108 files · ~38,582 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 14 file(s) not represented in the graph (top: .xml 3, .plist 3, .ttf 2)

## Summary
- 1426 nodes · 3170 edges · 71 communities (51 shown, 20 thin omitted)
- Extraction: 85% EXTRACTED · 15% INFERRED · 0% AMBIGUOUS · INFERRED: 473 edges (avg confidence: 0.71)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `e175e7b6`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- ContentPage
- .ConfigureServices
- PaymentsViewModel
- UserControl
- UserControl
- ResortSync.Data
- BookingFormViewModel
- UserControl
- ContentPage
- CancellationToken
- ReceiptService
- Booking
- Type
- UserControl
- INavigationService
- BookingsViewModel
- BookingDetailPage
- RemoteDoc
- IDialogService
- Task
- ResortSync.csproj
- CalendarViewModel
- MainViewModel
- ContentPage
- .RunBusyAsync
- BookingService
- SyncCoordinator
- AGENTS.md
- SyncStatus
- ResortSync.ViewModels
- ResortSync.Services
- Guest
- BookingAddon
- ResortSync.Models
- ResortDbContext
- FirestoreRestClient
- ContentPage
- .OnNavigatedToAsync
- DateTime
- BookingStatus
- ResourceDictionary
- system_windows_controls
- ResortSync.Mobile
- SettingsModel
- .ConfigureServices
- ResortSync/App.xaml.cs
- .ShowReceipt
- BookingsPage
- Windows/App.xaml.cs
- PRPMS — Private Resort Payment Monitoring System
- ReceiptService.cs
- CalendarDayCell
- Colors.xaml
- PaymentTransaction
- ContentPage
- .CreateMauiApp
- ContentPage
- App
- SyncState
- Addon
- App
- NavList
- BookingConflictException
- FirebaseAuthClient
- .LoadAsync
- SyncConfig.cs

## God Nodes (most connected - your core abstractions)
1. `Booking` - 82 edges
2. `UserControl` - 78 edges
3. `BookingFormViewModel` - 72 edges
4. `UserControl` - 64 edges
5. `BookingsViewModel` - 54 edges
6. `ResortDbContext` - 51 edges
7. `UserControl` - 51 edges
8. `CalendarViewModel` - 42 edges
9. `ResortSync.Services` - 40 edges
10. `Guest` - 38 edges

## Surprising Connections (you probably didn't know these)
- `How This App Was Built` --references--> `Addon`  [INFERRED]
  README.md → src/ResortSync.Core/Models/Addon.cs
- `How This App Was Built` --references--> `Booking`  [INFERRED]
  README.md → src/ResortSync.Core/Models/Booking.cs
- `How This App Was Built` --references--> `BookingAddon`  [INFERRED]
  README.md → src/ResortSync.Core/Models/BookingAddon.cs
- `How This App Was Built` --references--> `Guest`  [INFERRED]
  README.md → src/ResortSync.Core/Models/Guest.cs
- `How This App Was Built` --references--> `PaymentTransaction`  [INFERRED]
  README.md → src/ResortSync.Core/Models/PaymentTransaction.cs

## Import Cycles
- None detected.

## Communities (71 total, 20 thin omitted)

### Community 0 - "ContentPage"
Cohesion: 0.05
Nodes (34): PaymentMethod, BankTransfer, Cash, CreditCard, GCash, Maya, PaymentType, Downpayment (+26 more)

### Community 1 - ".ConfigureServices"
Cohesion: 0.06
Nodes (37): CloudStatus, DefaultBaseGuests, DefaultExtraGuestRate, DownpaymentPercent, FirebaseApiKey, FirebaseProjectId, ResortName, SyncEmail (+29 more)

### Community 2 - "PaymentsViewModel"
Cohesion: 0.06
Nodes (38): ExportCsvCommand, FromDate, MethodFilters, OpenBookingCommand, Rows, SelectedItem, SelectedMethodFilter, SelectedTypeFilter (+30 more)

### Community 3 - "UserControl"
Cohesion: 0.04
Nodes (49): Bookings, CancelBookingCommand, InverseBoolToVis, DataContext.VoidPaymentCommand, Detail.CheckInDate, Detail.CheckOutDate, Detail.DepositDeadline, Detail.Guest.Email (+41 more)

### Community 4 - "UserControl"
Cohesion: 0.04
Nodes (48): AdditionalGuests, Addons, AddonsTotal, BaseAmount, BaseGuests, CheckOutDate, ClearGuestCommand, ConflictMessage (+40 more)

### Community 6 - "BookingFormViewModel"
Cohesion: 0.05
Nodes (37): BookingFormViewModel, AdditionalGuests, Addons, AddonsTotal, BaseAmount, BaseGuests, CancelCommand, CheckInDate (+29 more)

### Community 7 - "UserControl"
Cohesion: 0.05
Nodes (40): ActiveBookings, DeadlineLabel, DataContext.OpenAlertCommand, DepositAlerts, DepositDeadline, IsDepositExpiringSoon, IsDepositOverdue, MonthlyRevenue (+32 more)

### Community 8 - "ContentPage"
Cohesion: 0.18
Nodes (18): AddonsStack, BalanceLabel, ContactLabel, ContentPage, DatesLabel, DeadlineLabel, DepositLabel, GuestLabel (+10 more)

### Community 10 - "ReceiptService"
Cohesion: 0.25
Nodes (4): IReceiptService, ReceiptService, ReceiptViewModel, Document

### Community 11 - "Booking"
Cohesion: 0.07
Nodes (29): Booking, AdditionalGuests, AddonsAmount, BaseAmount, BaseGuests, BookingAddons, CheckInDate, CheckOutDate (+21 more)

### Community 12 - "Type"
Cohesion: 0.15
Nodes (8): ResortSync.Converters, CurrencyConverter, DeadlineLabelConverter, InverseBoolConverter, InverseBoolToVisibilityConverter, IsEmptyToVisibilityConverter, StatusToBrushConverter, StatusColorConverter

### Community 13 - "UserControl"
Cohesion: 0.05
Nodes (39): AmountLabel, DataContext.OpenBookingCommand, DataContext.SelectDayCommand, DateRangeLabel, DayNumber, Days, GoTodayCommand, HasBookings (+31 more)

### Community 14 - "INavigationService"
Cohesion: 0.15
Nodes (14): INavigationService, Current, Items, SelectedItem, NavigationItem, NavigationService, Current, Items (+6 more)

### Community 15 - "BookingsViewModel"
Cohesion: 0.09
Nodes (20): BookingsViewModel, ApplyFiltersCommand, Bookings, CancelBookingCommand, Detail, DetailPaid, DetailPayments, EditBookingCommand (+12 more)

### Community 16 - "BookingDetailPage"
Cohesion: 0.23
Nodes (4): RecordPaymentButton, BookingDetailPage, BookingId, Button

### Community 18 - "IDialogService"
Cohesion: 0.18
Nodes (3): IPaymentService, DialogService, IDialogService

### Community 19 - "Task"
Cohesion: 0.09
Nodes (14): DbInitializer, SyncSchema, PaymentService, FakeFirestoreTransport, Commits, MigrationTests, SyncEngineTests, SyncStampingTests (+6 more)

### Community 20 - "ResortSync.csproj"
Cohesion: 0.07
Nodes (26): net8.0-android, net8.0-ios, net8.0-maccatalyst, net8.0-windows, net8.0-windows10.0.19041.0, $(TargetFrameworks), MaterialDesignThemes (5.1.0), Microsoft.Extensions.Hosting (8.0.0) (+18 more)

### Community 21 - "CalendarViewModel"
Cohesion: 0.11
Nodes (16): CalendarViewModel, Days, GoTodayCommand, IsSelected, Legend, MonthSummary, MonthTitle, NextMonthCommand (+8 more)

### Community 22 - "MainViewModel"
Cohesion: 0.22
Nodes (8): CurrentViewModel, CurrentViewModel.Title, GoHomeCommand, Icon, CurrentViewModel, Window, StatusMessage, Title

### Community 23 - "ContentPage"
Cohesion: 0.14
Nodes (19): ApiKeyEntry, BaseGuestsEntry, CloudStatusLabel, ContentPage, DownpaymentEntry, EmailEntry, ExtraRateEntry, PasswordEntry (+11 more)

### Community 25 - "BookingService"
Cohesion: 0.20
Nodes (7): OvernightStayType, DayTour, NightTour, Overnight, TwentyTwoHourExclusive, BookingService, IBookingService

### Community 27 - "SyncCoordinator"
Cohesion: 0.20
Nodes (8): Architecture, SyncCoordinator, LastResult, LastSyncAt, SyncWorker, CountingInvalidator, Count, SyncCoordinatorTests

### Community 30 - "SyncStatus"
Cohesion: 0.22
Nodes (9): SyncResult, Disabled, NotConfigured, SyncStatus, Disabled, Error, Idle, SignedOut (+1 more)

### Community 33 - "Guest"
Cohesion: 0.14
Nodes (13): Guest, Bookings, CloudId, DisplayName, Email, FullName, Id, IdNumber (+5 more)

### Community 34 - "BookingAddon"
Cohesion: 0.14
Nodes (11): BookingAddon, Addon, AddonId, Booking, BookingId, CloudId, Id, LineTotal (+3 more)

### Community 36 - "ResortDbContext"
Cohesion: 0.18
Nodes (10): ResortDbContext, Addons, BookingAddons, Bookings, Guests, PaymentTransactions, SyncApplyMode, SyncStates (+2 more)

### Community 37 - "FirestoreRestClient"
Cohesion: 0.20
Nodes (5): FirestoreRestClient, IFirebaseAuth, IFirestoreTransport, FakeFirebaseAuth, SignInCount

### Community 38 - "ContentPage"
Cohesion: 0.05
Nodes (45): AdditionalStepper, AddonsStack, AddonsTotalLabel, BaseAmountEntry, BaseGuestsStepper, CheckInPicker, CheckOutPicker, ConflictLabel (+37 more)

### Community 39 - ".OnNavigatedToAsync"
Cohesion: 0.29
Nodes (6): AddonOption, AddonId, LineTotal, Name, PriceLabel, UnitPrice

### Community 41 - "BookingStatus"
Cohesion: 0.20
Nodes (9): BookingStatus, Cancelled, Completed, Confirmed, Expired, FullyPaid, PendingDeposit, CalendarBookingChip (+1 more)

### Community 42 - "ResourceDictionary"
Cohesion: 0.29
Nodes (9): CommonStates, Disabled, Normal, Off, On, PointerOver, ResourceDictionary, VisualState (+1 more)

### Community 43 - "system_windows_controls"
Cohesion: 0.20
Nodes (6): BookingFormView, BookingsView, CalendarView, DashboardView, PaymentsView, SettingsView

### Community 44 - "ResortSync.Mobile"
Cohesion: 0.07
Nodes (10): ResortSync.Mobile, Shell, AppShell, MainActivity, MainApplication, AppDelegate, Program, AppDelegate (+2 more)

### Community 45 - "SettingsModel"
Cohesion: 0.11
Nodes (16): ISettingsService, SettingsModel, DefaultBaseGuests, DefaultExtraGuestRate, DeviceId, DownpaymentPercent, FirebaseApiKey, FirebaseProjectId (+8 more)

### Community 46 - ".ConfigureServices"
Cohesion: 0.22
Nodes (3): BookingExpiryService, DashboardInvalidator, IDashboardInvalidator

### Community 47 - "ResortSync/App.xaml.cs"
Cohesion: 0.24
Nodes (4): ResortSync, ResortSync.Constants, PricingCalculator, PricingResult

### Community 48 - ".ShowReceipt"
Cohesion: 0.22
Nodes (5): Document, PrintCommand, MainWindow, Window, ReceiptWindow

### Community 49 - "BookingsPage"
Cohesion: 0.20
Nodes (5): StatusPicker, BookingsPage, Picker, GuestSearch, SearchBar

### Community 50 - "Windows/App.xaml.cs"
Cohesion: 0.33
Nodes (3): ResortSync.Mobile.WinUI, MauiWinUIApplication, App

### Community 51 - "PRPMS — Private Resort Payment Monitoring System"
Cohesion: 0.17
Nodes (11): Credits, Data Locations, Developers, End users — Android, End users — desktop, Features, Getting the App, How This App Was Built (+3 more)

### Community 52 - "ReceiptService.cs"
Cohesion: 0.22
Nodes (5): AppConstants, ConnectionString, DatabasePath, DataDirectory, SettingsPath

### Community 53 - "CalendarDayCell"
Cohesion: 0.17
Nodes (11): CalendarDayCell, Bookings, Date, DayNumber, HasBookings, HasOverflow, IsCurrentMonth, IsToday (+3 more)

### Community 55 - "PaymentTransaction"
Cohesion: 0.05
Nodes (33): GuestSummary, Notes, OutstandingBalance, PaymentMethods, PaymentTypes, SetAmountCommand, SetHalfAmountCommand, ValidationMessage (+25 more)

### Community 56 - "ContentPage"
Cohesion: 0.12
Nodes (15): StatusColor, BookingsList, ContentPage, CountLabel, SearchEntry, CheckInDate, Guest.FullName, Id (+7 more)

### Community 58 - "ContentPage"
Cohesion: 0.11
Nodes (17): AmountEntry, ContentPage, DatePicker, HeadingLabel, MethodPicker, NotesEntry, ReferenceEntry, SaveButton (+9 more)

### Community 59 - "App"
Cohesion: 0.15
Nodes (3): Application, App, Services

### Community 60 - "SyncState"
Cohesion: 0.13
Nodes (11): SyncState, Collection, Cursor, LastSyncAt, PushedThrough, Tombstone, CloudId, Collection (+3 more)

### Community 61 - "Addon"
Cohesion: 0.18
Nodes (11): Addon, BookingAddons, CloudId, Id, IsActive, Name, UnitPrice, UpdatedAt (+3 more)

### Community 64 - "NavList"
Cohesion: 0.40
Nodes (5): NavItems, SelectedNavItem, SelectedNavItem, NavList, ListBox

### Community 72 - "SyncConfig.cs"
Cohesion: 0.33
Nodes (4): SyncConfig, Empty, IsConfigured, SyncConfigExtensions

## Knowledge Gaps
- **490 isolated node(s):** `DataDirectory`, `DatabasePath`, `ConnectionString`, `SettingsPath`, `SyncApplyMode` (+485 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 582 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **20 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Booking` connect `Booking` to `BookingFormViewModel`, `UserControl`, `ReceiptService`, `BookingsViewModel`, `BookingDetailPage`, `RemoteDoc`, `IDialogService`, `Task`, `CalendarViewModel`, `BookingService`, `Guest`, `BookingAddon`, `ResortSync.Models`, `ResortDbContext`, `ContentPage`, `DateTime`, `BookingStatus`, `.ShowReceipt`, `BookingsPage`, `PRPMS — Private Resort Payment Monitoring System`, `PaymentTransaction`, `Addon`, `Task`?**
  _High betweenness centrality (0.180) - this node is a cross-community bridge._
- **Are the 5 inferred relationships involving `Booking` (e.g. with `How This App Was Built` and `.OnSaveClicked()`) actually correct?**
  _`Booking` has 5 INFERRED edges - model-reasoned connections that need verification._
- **What connects `DataDirectory`, `DatabasePath`, `ConnectionString` to the rest of the system?**
  _490 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `ContentPage` be split into smaller, more focused modules?**
  _Cohesion score 0.0512987012987013 - nodes in this community are weakly interconnected._
- **Why does `BookingFormViewModel` connect `BookingFormViewModel` to `Guest`, `Task`, `UserControl`, `UserControl`, `DateTime`, `.OnNavigatedToAsync`, `Booking`, `SettingsModel`, `INavigationService`, `BookingsPage`, `IDialogService`, `BookingService`, `.ValidateDatesAsync`, `.Recalculate`, `ResortSync.ViewModels`?**
  _High betweenness centrality (0.128) - this node is a cross-community bridge._
- **Are the 29 inferred relationships involving `UserControl` (e.g. with `BookingFormViewModel` and `AdditionalGuests`) actually correct?**
  _`UserControl` has 29 INFERRED edges - model-reasoned connections that need verification._
- **Should `.ConfigureServices` be split into smaller, more focused modules?**
  _Cohesion score 0.06342494714587738 - nodes in this community are weakly interconnected._