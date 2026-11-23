# PRPMS — Private Resort Payment Monitoring System

[![GitHub](https://img.shields.io/badge/GitHub-teryoooow%2Fprpms-181717?logo=github)](https://github.com/teryoooow/prpms)

A production-ready **Windows desktop + Android mobile** application suite for monitoring
payments at a private resort. It tracks bookings, enforces the 24-hour downpayment rule,
records payments (downpayments, balance settlements, security deposits, refunds), prints
receipts, and shows live revenue and collection stats — with a modern Material Design
interface on desktop and a MAUI app on Android. Both sides **two-way sync** through
Firebase Firestore (Spark/free tier), so the front desk and staff on the floor always
see the same data.

**Version:** 1.0.2  
**Platforms:** Windows 10/11 (x64) · Android 7.0+ (API 24+)  
**Credit / Author:** **Terelle Sarmiento** ([@teryoooow](https://github.com/teryoooow))

---

## Features

- **Dashboard** — monthly revenue, balances to collect, today's check-ins, overdue
  downpayments, and a live alert list of bookings inside or past the 24-hour deposit window
- **Booking management** — create/edit bookings with guest type-ahead search, stay type
  (overnight / 22-hour exclusive / tour), add-on catalog lines, and live price calculation
- **Booking calendar** — month view of every occupied date with booking chips colored
  Pending (amber) or Complete (brown); click a date to list its bookings and open them
- **Double-booking protection** — authoritative date-conflict check at save time
- **Payment ledger** — record payments, void mistakes (audit-safe), filter/search/export to CSV
- **Automatic expiration** — a background worker flips overdue `PendingDeposit` bookings to
  `Expired` (releasing their dates) every 60 seconds; the 24-hour window is configurable
- **Status lifecycle** — `PendingDeposit → Confirmed → FullyPaid → Completed`, plus
  `Cancelled` / `Expired`, with a legal-transition guard
- **Receipts** — printable receipt via the system print dialog
- **Settings** — resort name, downpayment percentage (default 50%), default extra-guest
  rate, light/dark theme (persisted), plus cloud-sync credentials (Firebase API key,
  project id, sync account) with a status line and **Sync Now**
- **Offline-first** — SQLite database stored locally; nothing leaves the machine until
  cloud sync is enabled
- **Two-way cloud sync** — desktop and Android share one Firestore project; last-write-wins
  on `updated_at`, hard deletes propagate as tombstones, sync every 60s + on app resume,
  manual **Sync Now** in Settings

## Tech Stack

| Layer | Choice |
|---|---|
| Desktop UI | WPF (.NET 8), MaterialDesignInXAML 5.1 |
| Mobile UI | .NET MAUI (`net8.0-android`; iOS/MacCatalyst TFMs ready for a Mac host) |
| Pattern | MVVM (CommunityToolkit.Mvvm) |
| Data | EF Core 8 + SQLite (`IDbContextFactory` pattern) |
| Sync | Firebase Auth + Firestore REST (no mobile SDK) via `ResortSync.Core` |
| Hosting | Generic Host on desktop; manual worker start on MAUI |
| Installers | Inno Setup 6 (desktop) · signed APK (Android) |

## Architecture

```
src/
├── ResortSync.Core/         Shared domain + sync (referenced by desktop AND mobile)
│   ├── Data/                ResortDbContext + DbInitializer
│   ├── Models/              Guest, Booking, PaymentTransaction, Addon, …
│   ├── Services/            BookingService, PaymentService, DashboardService,
│   │                        BookingExpiryService, SettingsService, …
│   └── Sync/                SyncEngine, SyncCoordinator, SyncWorker,
│                            FirebaseAuthClient, FirestoreRestClient, DocMapper
├── ResortSync/              WPF desktop shell (Generic Host DI, MaterialDesign views)
└── ResortSync.Mobile/       .NET MAUI app (Bookings / Payments / Settings tabs)
    ├── MauiProgram.cs       DI + DbInitializer + manual worker StartAsync
    └── Views/               BookingsPage, BookingDetailPage, NewBookingPage,
                             RecordPaymentPage, PaymentsPage, SettingsPage

installer/
├── ResortSync.iss           Inno Setup script (desktop)
├── build-installer.ps1      One-shot publish + compile pipeline
├── build-apk.ps1            Signed Release APK (reads keystore.local.env)
└── keystore/                [gitignored] Android signing key + local env
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
- **Sync pulls full collections every cycle** — resort-scale data is tiny; filtering by an
  `updated_at` watermark skips remote rows older than the local max even when never seen
  (first-sync skew). LWW makes re-applying known docs a no-op.
- **Mobile workers started manually** — MAUI has no Generic Host, so `BookingExpiryService`
  and `SyncWorker` are singletons `StartAsync`'d after `builder.Build()`; `OnResume` also
  calls `SyncCoordinator.RequestSync()`.

## Getting the App

### End users — desktop
Download and run **`PRPMS-Setup-1.0.2.exe`**. It installs to `Program Files\PRPMS`,
creates Start Menu / desktop shortcuts, and needs nothing else installed.
Your data lives in `%LocalAppData%\ResortSync\` and is kept on uninstall.

### End users — Android
Install the signed APK from `installer\Output\com.terelle.prpms-Signed.apk`
(sideload; package `com.terelle.prpms`). Open **Settings → Cloud Sync**, enter the Firebase
API key / project id / sync account, enable sync, then **Sync Now**.

### Developers
Requires the .NET 8 SDK (+ workloads for mobile: `dotnet workload install android maui`).

```powershell
git clone https://github.com/teryoooow/prpms.git
cd prpms
dotnet build ResortSync.sln -c Release        # build desktop + core
dotnet run --project src\ResortSync          # run desktop from source
dotnet test tests\ResortSync.Tests -c Release # 24 unit tests
```

Build the desktop installer:

```powershell
powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
# → installer\Output\PRPMS-Setup-1.0.2.exe
```

Build the signed Android APK:

```powershell
# One-time: create installer\keystore\prpms.keystore with keytool and write
# KEYSTORE_PATH / KEYSTORE_ALIAS / KEYSTORE_PASSWORD into
# installer\keystore\keystore.local.env  (both paths are gitignored)
powershell -ExecutionPolicy Bypass -File installer\build-apk.ps1
# → installer\Output\com.terelle.prpms-Signed.apk  (copied from the build output)
```

Debug install on a connected device (must use `-p:EmbedAssembliesIntoApk=true`):

```powershell
dotnet build src\ResortSync.Mobile -f net8.0-android -c Debug -p:EmbedAssembliesIntoApk=true
adb install -r src\ResortSync.Mobile\bin\Debug\net8.0-android\com.terelle.prpms-Signed.apk
```

> **Note:** the desktop publish step is routed through `%TEMP%` inside `build-installer.ps1`
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
10. **Mobile + sync** — shared domain/sync extracted to `ResortSync.Core`; a .NET MAUI
    Android app (Bookings / Payments / Settings) talks to Firebase Auth + Firestore over
    REST (no mobile SDK); last-write-wins sync engine with tombstones, a 60-second worker,
    and on-resume sync; signed release APK plus 24 unit tests covering the engine,
    coordinator, sync stamping, and schema migrations.

## Data Locations

| What | Where |
|---|---|
| Desktop database | `%LocalAppData%\ResortSync\resortsync.db` (SQLite, WAL mode) |
| Desktop settings | `%LocalAppData%\ResortSync\settings.json` |
| Android database | `./files/ResortSync/resortsync.db` (app-private) |
| Android settings | `./files/ResortSync/settings.json` (app-private) |
| CSV exports | Chosen by the user via save dialog |
| Cloud | Firebase Firestore `prpms-7d462` — collections `bookings`, `guests`, `payments`, `addons`, `booking_addons`, `settings` (doc id `app`) |

## Credits

**Created by Terelle Sarmiento.**

Built with .NET 8, WPF, .NET MAUI, Entity Framework Core, MaterialDesignInXAML
(Material Design Icons community), CommunityToolkit.Mvvm, Firebase (Auth + Firestore REST),
and Inno Setup.

## License

All rights reserved by the author unless otherwise stated.
