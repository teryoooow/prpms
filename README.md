# PRPMS — Private Resort Payment Monitoring System

A production-ready **Windows desktop application** for monitoring payments at a private
resort. It tracks bookings, enforces the 24-hour downpayment rule, records payments
(downpayments, balance settlements, security deposits, refunds), prints receipts, and
shows live revenue and collection stats — with a modern Material Design interface.

**Version:** 1.0.0  
**Platform:** Windows 10/11 (x64) — self-contained installer, no .NET prerequisite needed  
**Credit / Author:** **Terelle Sarmiento** ([@teryoooow](https://github.com/teryoooow))

---

## Features

- **Dashboard** — monthly revenue, balances to collect, today's check-ins, overdue
  downpayments, and a live alert list of bookings inside or past the 24-hour deposit window
- **Booking management** — create/edit bookings with guest type-ahead search, stay type
  (overnight / 22-hour exclusive / tour), add-on catalog lines, and live price calculation
- **Double-booking protection** — authoritative date-conflict check at save time
- **Payment ledger** — record payments, void mistakes (audit-safe), filter/search/export to CSV
- **Automatic expiration** — a background worker flips overdue `PendingDeposit` bookings to
  `Expired` (releasing their dates) every 60 seconds; the 24-hour window is configurable
- **Status lifecycle** — `PendingDeposit → Confirmed → FullyPaid → Completed`, plus
  `Cancelled` / `Expired`, with a legal-transition guard
- **Receipts** — printable receipt via the system print dialog
- **Settings** — resort name, downpayment percentage (default 50%), default extra-guest
  rate, light/dark theme (persisted)
- **Offline-first** — SQLite database stored locally; nothing leaves the machine

## Tech Stack

| Layer | Choice |
|---|---|
| UI | WPF (.NET 8), MaterialDesignInXAML 5.1 |
| Pattern | MVVM (CommunityToolkit.Mvvm 8.2 — `[ObservableProperty]`, `[RelayCommand]`) |
| Data | EF Core 8 + SQLite (`IDbContextFactory` pattern) |
| Hosting | Generic Host (DI + hosted background service) |
| Installer | Inno Setup 6 (self-contained win-x64 publish) |

## Architecture

```
src/ResortSync/
├── App.xaml(.cs)          Generic Host DI wiring, theme, VM→View DataTemplates,
│                          global dispatcher exception guard, DB init on startup
├── Constants/             Business constants (deposit hours, expiry interval, paths)
├── Data/                  ResortDbContext + DbInitializer (schema + seed)
├── Models/                Guest, Booking, PaymentTransaction, Addon, BookingAddon + enums
├── Services/              BookingService   — pricing, conflicts, save/edit, status rules
│                          PaymentService   — payments, balance recompute, ledger query
│                          DashboardService — stats + deposit alerts
│                          BookingExpiryService — PeriodicTimer background worker
│                          ReceiptService / ThemeService / SettingsService
│                          DialogService / NavigationService / DashboardInvalidator
├── ViewModels/            MainViewModel (shell/drawer), Dashboard, BookingForm,
│                          Bookings, Payments, Settings, PaymentDialog, Receipt
├── Views/                 XAML views + MainWindow shell with navigation drawer
├── Converters/            Status→brush, currency, deadline label, visibility helpers
└── Assets/app.ico         Generated multi-size application/installer icon
installer/
├── ResortSync.iss         Inno Setup script
└── build-installer.ps1    One-shot publish + compile pipeline
```

Key design decisions:

- **`IDbContextFactory<ResortDbContext>`** — short-lived contexts per operation instead of
  one long-lived context (the recommended WPF pattern; avoids cross-thread and identity-map
  pitfalls).
- **Read-only queries use `AsNoTracking`** — e.g. the conflict-check load, so a later
  `Attach` of the guest being saved never collides with a tracked instance.
- **Background worker uses `Task.Run`** — the host starts on the WPF dispatcher thread; the
  thread-pool loop prevents shutdown deadlocks and marshals UI refreshes via
  `IDashboardInvalidator` + `Dispatcher.InvokeAsync`.
- **Money computed server-side** — the form's live calculator is advisory; the authoritative
  totals are recomputed in `BookingService.SaveBookingAsync` from source inputs.
- **SQLite stored as text decimals** — sums are performed client-side after materialization.

## Getting the App

### End users
Download and run **`PRPMS-Setup-1.0.0.exe`**. It installs to `Program Files\PRPMS`,
creates Start Menu / desktop shortcuts, and needs nothing else installed.
Your data lives in `%LocalAppData%\ResortSync\` and is kept on uninstall.

### Developers
Requires the .NET 8 SDK.

```powershell
git clone https://github.com/teryoooow/prpms.git
cd prpms
dotnet build ResortSync.sln -c Release        # build
dotnet run --project src\ResortSync          # run from source
```

Build the installer:

```powershell
powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
# → installer\Output\PRPMS-Setup-1.0.0.exe
```

> **Note:** the publish step is routed through `%TEMP%` inside `build-installer.ps1`
> because MSBuild's publish copy target mishandles apostrophes in paths (this project
> originally lived under a folder name containing one).

## How This App Was Built

1. **Requirements** — private-resort payment monitoring: bookings with a 24-hour
   downpayment rule, partial payments, automatic expiration of unpaid bookings, and a
   real-time collection dashboard.
2. **Solution design** — MVVM with a Generic Host for dependency injection; a navigation
   drawer shell that swaps ViewModels in the content area via implicit `DataTemplate`s.
3. **Domain model** — `Booking` (stay window, status, money fields, 24-hour deposit
   deadline), `Guest` (reusable via type-ahead), `PaymentTransaction`
   (downpayment/final balance/security deposit/refund), `Addon` + `BookingAddon` lines.
4. **Persistence** — EF Core 8 with SQLite; schema auto-created on first run
   (`EnsureCreated`, migrations documented as the upgrade path); enums stored as strings.
5. **Business rules** — pricing calculator (base + extra guests + addons, 50% deposit),
   overlap conflict detection (range stays and same-day tours), status transition guard,
   balance recomputation that excludes security deposits and refunds from the stay balance.
6. **Background automation** — `BookingExpiryService` hosted worker checks every 60
   seconds and flips overdue bookings to `Expired`, releasing their inventory.
7. **UI** — Material Design 3 (dark teal theme), dashboard stat cards, master/detail
   booking views, payment dialog, printable receipt, settings page.
8. **Hardening & QA** — headless smoke-test harness exercising the full service layer
   (pricing math, deadline windows, conflict guards, status transitions, live expiry
   worker); targeted reproduction probes for EF Core issues; crash-dump analysis with
   `dotnet-dump`; unhandled-exception guard with a re-entrancy latch.
9. **Packaging** — self-contained win-x64 publish + Inno Setup installer with a generated
   multi-size app icon, silent-install support, and data-preserving uninstall.

## Data Locations

| What | Where |
|---|---|
| Database | `%LocalAppData%\ResortSync\resortsync.db` (SQLite, WAL mode) |
| Settings | `%LocalAppData%\ResortSync\settings.json` |
| CSV exports | Chosen by the user via save dialog |

## Credits

**Created by Terelle Sarmiento.**

Built with .NET 8, WPF, Entity Framework Core, MaterialDesignInXAML
(Material Design Icons community), CommunityToolkit.Mvvm, and Inno Setup.

## License

All rights reserved by the author unless otherwise stated.
