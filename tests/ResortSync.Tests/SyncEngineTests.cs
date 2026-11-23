using Microsoft.EntityFrameworkCore;
using ResortSync.Data;
using ResortSync.Models;
using ResortSync.Services;
using ResortSync.Sync;
using Xunit;

namespace ResortSync.Tests;

/// <summary>
/// Two simulated devices (separate SQLite files) sharing one in-memory cloud.
/// </summary>
public class SyncEngineTests
{
    private static async Task<TestDb> DeviceAsync()
    {
        var test = new TestDb();
        test.Initialize();
        await test.EnableSyncAsync();
        return test;
    }

    private static void AssertIdle(SyncResult r)
        => Assert.True(r.State == SyncStatus.Idle, $"sync failed: {r.Error}");

    private static async Task<Booking> CreateBookingAsync(TestDb test, bool withAddon = true)
    {
        using var db = test.CreateContext();
        var addonId = db.Addons.First(a => a.Name == "Videoke").Id;

        var service = new BookingService(test.Factory, test.Settings);
        return await service.SaveBookingAsync(
            new Guest { FullName = "Test Guest", Phone = "09990001112" },
            new Booking
            {
                CheckInDate = new DateTime(2026, 12, 1, 14, 0, 0),
                CheckOutDate = new DateTime(2026, 12, 2, 12, 0, 0),
                OvernightStayType = OvernightStayType.Overnight,
                BaseAmount = 5000m
            },
            withAddon
                ? new[] { new BookingAddonInput(addonId, 1, 500m) }
                : Array.Empty<BookingAddonInput>());
    }

    private static Task<int> PaymentIdAsync(TestDb test)
    {
        using var db = test.CreateContext();
        return Task.FromResult(db.PaymentTransactions.Single().Id);
    }

    [Fact]
    public async Task CreatedBooking_ArrivesOnSecondDevice()
    {
        using var a = await DeviceAsync();
        using var b = await DeviceAsync();
        var cloud = new FakeFirestoreTransport();

        var booking = await CreateBookingAsync(a);

        AssertIdle(await a.SyncAsync(cloud));
        AssertIdle(await b.SyncAsync(cloud));

        using var bdb = b.CreateContext();
        var guest = bdb.Guests.Single(g => g.FullName == "Test Guest");
        var copied = bdb.Bookings.Single();
        var line = bdb.BookingAddons.Single();

        Assert.Equal(booking.CloudId, copied.CloudId);
        Assert.Equal(booking.TotalAmount, copied.TotalAmount);
        Assert.Equal(booking.Guest.CloudId, guest.CloudId);
        Assert.Equal("Videoke", bdb.Addons.Single(x => x.Id == line.AddonId).Name);
        Assert.Equal(booking.UpdatedAt, copied.UpdatedAt); // stamps travel verbatim
    }

    [Fact]
    public async Task NewerEditOnDeviceB_OverwritesDeviceA()
    {
        using var a = await DeviceAsync();
        using var b = await DeviceAsync();
        var cloud = new FakeFirestoreTransport();

        await CreateBookingAsync(a, withAddon: false);
        AssertIdle(await a.SyncAsync(cloud));
        AssertIdle(await b.SyncAsync(cloud));

        await Task.Delay(30);
        using (var bdb = b.CreateContext())
        {
            var bk = bdb.Bookings.Include(x => x.Guest).Single();
            bk.Notes = "edited on B";
            bdb.SaveChanges(); // app stamping bumps UpdatedAt
        }

        AssertIdle(await b.SyncAsync(cloud));
        AssertIdle(await a.SyncAsync(cloud));

        using var adb = a.CreateContext();
        Assert.Equal("edited on B", adb.Bookings.Single().Notes);
    }

    [Fact]
    public async Task OlderLocalStamp_DoesNotOverwriteNewerRemote()
    {
        using var a = await DeviceAsync();
        using var b = await DeviceAsync();
        var cloud = new FakeFirestoreTransport();

        await CreateBookingAsync(a, withAddon: false);
        AssertIdle(await a.SyncAsync(cloud));
        AssertIdle(await b.SyncAsync(cloud));

        using (var adb = a.CreateContext())
        {
            var bk = adb.Bookings.Single();
            adb.SyncApplyMode = true; // bypass stamping to force a stale edit
            bk.Notes = "stale A";
            bk.UpdatedAt = DateTime.UtcNow.AddHours(-1);
            adb.SaveChanges();
        }

        using (var bdb = b.CreateContext())
        {
            var bk = bdb.Bookings.Single();
            bdb.SyncApplyMode = true;
            bk.Notes = "fresh B";
            bk.UpdatedAt = DateTime.UtcNow.AddHours(1);
            bdb.SaveChanges();
        }

        AssertIdle(await b.SyncAsync(cloud));
        AssertIdle(await a.SyncAsync(cloud));

        using var adb2 = a.CreateContext();
        Assert.Equal("fresh B", adb2.Bookings.Single().Notes);
    }

    [Fact]
    public async Task VoidedPayment_DisappearsOnOtherDevice()
    {
        using var a = await DeviceAsync();
        using var b = await DeviceAsync();
        var cloud = new FakeFirestoreTransport();

        var booking = await CreateBookingAsync(a, withAddon: false);
        var payments = new PaymentService(a.Factory);
        await payments.RecordPaymentAsync(booking.Id, new PaymentTransaction
        {
            PaymentType = PaymentType.Downpayment,
            PaymentMethod = PaymentMethod.Cash,
            AmountPaid = 2500m
        });

        AssertIdle(await a.SyncAsync(cloud));
        AssertIdle(await b.SyncAsync(cloud));

        await payments.VoidPaymentAsync(await PaymentIdAsync(a));
        AssertIdle(await a.SyncAsync(cloud)); // pushes the is_deleted marker
        AssertIdle(await b.SyncAsync(cloud));

        using var bdb = b.CreateContext();
        Assert.Empty(bdb.PaymentTransactions);
        using var adb = a.CreateContext();
        Assert.Empty(adb.Tombstones); // acked tombstones are retired
    }

    [Fact]
    public async Task PendingTombstone_BlocksOlderRemoteRow_AndPushesDelete()
    {
        using var a = await DeviceAsync();
        using var b = await DeviceAsync();
        var cloud = new FakeFirestoreTransport();

        var booking = await CreateBookingAsync(a, withAddon: false);
        var payments = new PaymentService(a.Factory);
        await payments.RecordPaymentAsync(booking.Id, new PaymentTransaction
        {
            PaymentType = PaymentType.Downpayment,
            PaymentMethod = PaymentMethod.Cash,
            AmountPaid = 2500m
        });

        AssertIdle(await a.SyncAsync(cloud));
        AssertIdle(await b.SyncAsync(cloud)); // cloud row is now at T0

        string paymentCloudId;
        using (var adb = a.CreateContext())
        {
            paymentCloudId = adb.PaymentTransactions.Single().CloudId!;
        }

        await payments.VoidPaymentAsync(await PaymentIdAsync(a)); // tombstone @ T1 > T0
        AssertIdle(await a.SyncAsync(cloud));

        using var adb2 = a.CreateContext();
        Assert.Empty(adb2.PaymentTransactions);
        Assert.Empty(adb2.Tombstones); // delete was pushed as a marker

        var stored = cloud.Get(SyncCollections.Payments, paymentCloudId);
        Assert.NotNull(stored);
        Assert.True(stored!.TryGetValue("is_deleted", out var del) && del is true);

        AssertIdle(await b.SyncAsync(cloud));
        using var bdb = b.CreateContext();
        Assert.Empty(bdb.PaymentTransactions); // marker propagates
    }

    [Fact]
    public async Task NewerRemoteRow_RecreatesVoidedPayment_AndDropsTombstone()
    {
        using var a = await DeviceAsync();
        using var b = await DeviceAsync();
        var cloud = new FakeFirestoreTransport();

        var booking = await CreateBookingAsync(a, withAddon: false);
        var payments = new PaymentService(a.Factory);
        await payments.RecordPaymentAsync(booking.Id, new PaymentTransaction
        {
            PaymentType = PaymentType.Downpayment,
            PaymentMethod = PaymentMethod.Cash,
            AmountPaid = 2500m
        });

        AssertIdle(await a.SyncAsync(cloud));
        AssertIdle(await b.SyncAsync(cloud));

        string paymentCloudId;
        Dictionary<string, object?> wireFields;
        using (var adb = a.CreateContext())
        {
            var p = adb.PaymentTransactions.Include(x => x.Booking).Single();
            paymentCloudId = p.CloudId!;
            wireFields = DocMapper.Fields(p, p.Booking.CloudId!);
        }

        // A concurrent remote edit lands AFTER (in stamp terms) our delete:
        wireFields["updated_at"] = FirestoreValues.ToFirestoreUtc(DateTime.UtcNow.AddHours(1));
        cloud.Seed(SyncCollections.Payments, paymentCloudId, wireFields);

        await payments.VoidPaymentAsync(await PaymentIdAsync(a)); // tombstone @ now
        AssertIdle(await a.SyncAsync(cloud)); // pull recreates; tombstone retired

        using var adb2 = a.CreateContext();
        Assert.Single(adb2.PaymentTransactions); // row is back
        Assert.Empty(adb2.Tombstones);

        var stored = cloud.Get(SyncCollections.Payments, paymentCloudId);
        Assert.NotNull(stored);
        Assert.False(stored!.TryGetValue("is_deleted", out var del) && del is true);
    }

    [Fact]
    public async Task SecondSync_IsANoOp()
    {
        using var a = await DeviceAsync();
        using var b = await DeviceAsync();
        var cloud = new FakeFirestoreTransport();

        await CreateBookingAsync(a);
        AssertIdle(await a.SyncAsync(cloud));
        AssertIdle(await b.SyncAsync(cloud));
        AssertIdle(await a.SyncAsync(cloud));
        AssertIdle(await b.SyncAsync(cloud));

        var ra = await a.SyncAsync(cloud);
        AssertIdle(ra);
        Assert.Equal(0, ra.Pushed);
        Assert.Equal(0, ra.Pulled);
        Assert.Equal(0, ra.Deleted);

        var rb = await b.SyncAsync(cloud);
        AssertIdle(rb);
        Assert.Equal(0, rb.Pushed);
        Assert.Equal(0, rb.Pulled);
        Assert.Equal(0, rb.Deleted);
    }
    [Fact]
    public async Task SettingsChanges_ReachOtherDevice()
    {
        using var a = await DeviceAsync();
        using var b = await DeviceAsync();
        var cloud = new FakeFirestoreTransport();

        var s = await a.Settings.LoadAsync();
        s.ResortName = "Eden Resort";
        s.DownpaymentPercent = 0.60m;
        s.SettingsUpdatedAt = DateTime.UtcNow;
        await a.Settings.SaveAsync(s);

        AssertIdle(await a.SyncAsync(cloud));
        AssertIdle(await b.SyncAsync(cloud));

        var bs = await b.Settings.LoadAsync();
        Assert.Equal("Eden Resort", bs.ResortName);
        Assert.Equal(0.60m, bs.DownpaymentPercent);
    }

    [Fact]
    public async Task DisabledSync_ReturnsDisabled()
    {
        using var a = await DeviceAsync();
        var s = await a.Settings.LoadAsync();
        s.SyncEnabled = false;
        await a.Settings.SaveAsync(s);

        var r = await a.SyncAsync(new FakeFirestoreTransport());
        Assert.Equal(SyncStatus.Disabled, r.State);
    }

    [Fact]
    public async Task MissingCredentials_ReturnsSignedOut()
    {
        using var a = await DeviceAsync();
        var s = await a.Settings.LoadAsync();
        s.FirebaseApiKey = string.Empty;
        await a.Settings.SaveAsync(s);

        var r = await a.SyncAsync(new FakeFirestoreTransport());
        Assert.Equal(SyncStatus.SignedOut, r.State);
    }
}
