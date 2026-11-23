using Microsoft.EntityFrameworkCore;
using ResortSync.Data;
using ResortSync.Models;
using ResortSync.Services;
using Xunit;

namespace ResortSync.Tests;

public class SyncStampingTests
{
    private static SettingsService NewSettings(TestDb test)
        => new(Path.Combine(test.Dir, "settings.json"));

    private static async Task<Booking> CreateBookingAsync(TestDb test)
    {
        var service = new BookingService(test.Factory, NewSettings(test));
        return await service.SaveBookingAsync(
            new Guest { FullName = "Test Guest", Phone = "09990001112" },
            new Booking
            {
                CheckInDate = new DateTime(2026, 12, 1, 14, 0, 0),
                CheckOutDate = new DateTime(2026, 12, 2, 12, 0, 0),
                OvernightStayType = OvernightStayType.Overnight,
                BaseAmount = 5000m
            },
            Array.Empty<BookingAddonInput>());
    }

    [Fact]
    public async Task NewBooking_ReceivesCloudId_AndUtcTimestamp()
    {
        using var test = new TestDb();
        test.Initialize();

        var booking = await CreateBookingAsync(test);

        Assert.Matches("^[0-9a-f]{32}$", booking.CloudId);
        Assert.Equal(DateTimeKind.Utc, booking.UpdatedAt.Kind);
        Assert.True(booking.UpdatedAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task EditedBooking_BumpsUpdatedAt_InDatabase()
    {
        using var test = new TestDb();
        test.Initialize();

        var booking = await CreateBookingAsync(test);
        var firstStamp = booking.UpdatedAt;

        await Task.Delay(30); // ensure the clock moves even at coarse resolution

        var service = new BookingService(test.Factory, NewSettings(test));
        booking.Notes = "edited";
        await service.SaveBookingAsync(booking.Guest, booking, Array.Empty<BookingAddonInput>());

        using var db = test.CreateContext();
        var stored = db.Bookings.AsNoTracking().Single(b => b.Id == booking.Id);
        Assert.Equal("edited", stored.Notes);
        Assert.True(stored.UpdatedAt > firstStamp);
    }

    [Fact]
    public async Task VoidPayment_ProducesTombstone_AndRemovesRow()
    {
        using var test = new TestDb();
        test.Initialize();

        var booking = await CreateBookingAsync(test);

        var payments = new PaymentService(test.Factory);
        await payments.RecordPaymentAsync(booking.Id, new PaymentTransaction
        {
            PaymentType = PaymentType.Downpayment,
            PaymentMethod = PaymentMethod.Cash,
            AmountPaid = 2500m
        });

        string cloudId;
        int paymentId;
        using (var db = test.CreateContext())
        {
            var payment = db.PaymentTransactions.Single();
            paymentId = payment.Id;
            cloudId = payment.CloudId!;
        }

        await payments.VoidPaymentAsync(paymentId);

        using (var db = test.CreateContext())
        {
            Assert.Empty(db.PaymentTransactions);
            var tombstone = Assert.Single(db.Tombstones);
            Assert.Equal(cloudId, tombstone.CloudId);
            Assert.Equal(SyncCollections.Payments, tombstone.Collection);
        }
    }

    [Fact]
    public async Task AddonLineRebuild_TombstonesOldLine()
    {
        using var test = new TestDb();
        test.Initialize();

        var addonId = test.CreateContext().Addons.First(a => a.Name == "Videoke").Id;

        var service = new BookingService(test.Factory, NewSettings(test));
        var booking = await service.SaveBookingAsync(
            new Guest { FullName = "Addon Guest" },
            new Booking
            {
                CheckInDate = new DateTime(2026, 12, 5, 14, 0, 0),
                CheckOutDate = new DateTime(2026, 12, 6, 12, 0, 0),
                OvernightStayType = OvernightStayType.Overnight,
                BaseAmount = 5000m
            },
            new[] { new BookingAddonInput(addonId, 1, 500m) });

        string oldLineCloudId;
        using (var db = test.CreateContext())
        {
            oldLineCloudId = db.BookingAddons.Single().CloudId!;
        }

        // Rebuild the form's selection (drop the add-on) → old line hard-deleted.
        await service.SaveBookingAsync(booking.Guest, booking, Array.Empty<BookingAddonInput>());

        using (var db = test.CreateContext())
        {
            Assert.Empty(db.BookingAddons);
            var tombstone = Assert.Single(db.Tombstones);
            Assert.Equal(oldLineCloudId, tombstone.CloudId);
            Assert.Equal(SyncCollections.BookingAddons, tombstone.Collection);
        }
    }

    [Fact]
    public void SyncApplyMode_PreservesRemoteTimestamps()
    {
        using var test = new TestDb();
        test.Initialize();

        var remoteTime = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        using (var db = test.CreateContext())
        {
            db.SyncApplyMode = true; // the sync engine is applying a remote row
            db.Guests.Add(new Guest
            {
                FullName = "Remote Guest",
                CloudId = "abcdef0123456789abcdef0123456789",
                UpdatedAt = remoteTime
            });
            db.SaveChanges();
        }

        using (var db = test.CreateContext())
        {
            var guest = db.Guests.Single();
            Assert.Equal("abcdef0123456789abcdef0123456789", guest.CloudId);
            Assert.Equal(remoteTime, guest.UpdatedAt); // NOT bumped to "now"
        }
    }

    [Fact]
    public void AppWrites_StampCloudIdAndNow()
    {
        using var test = new TestDb();
        test.Initialize();

        using var db = test.CreateContext();
        db.Guests.Add(new Guest { FullName = "Local Guest" });
        db.SaveChanges();

        var guest = db.Guests.Single();
        Assert.Matches("^[0-9a-f]{32}$", guest.CloudId);
        Assert.True(guest.UpdatedAt > DateTime.UtcNow.AddMinutes(-1));
    }
}
