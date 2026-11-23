using ResortSync.Models;
using ResortSync.Services;
using ResortSync.Sync;
using Xunit;

namespace ResortSync.Tests;

public class SyncCoordinatorTests
{
    private sealed class CountingInvalidator : IDashboardInvalidator
    {
        public int Count { get; private set; }

        public event EventHandler? RefreshRequested;

        public void RequestRefresh()
        {
            Count++;
            RefreshRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    [Fact]
    public async Task SyncWithWork_RequestsUiRefresh_SyncWithoutWork_DoesNot()
    {
        using var device = await TestDeviceAsync();
        var cloud = new FakeFirestoreTransport();
        var invalidator = new CountingInvalidator();
        var coordinator = new SyncCoordinator(
            device.CreateEngine(cloud), device.Settings, invalidator);

        // Seed data on the device, then run a cycle that actually moves it.
        await CreateBookingAsync(device);
        var first = await coordinator.SyncNowAsync();
        Assert.Equal(SyncStatus.Idle, first.State);
        Assert.True(first.Pushed > 0);
        Assert.Equal(1, invalidator.Count);

        // A quiet cycle must not poke the UI.
        var second = await coordinator.SyncNowAsync();
        Assert.Equal(SyncStatus.Idle, second.State);
        Assert.Equal(0, second.Pushed + second.Pulled + second.Deleted);
        Assert.Equal(1, invalidator.Count);
    }

    [Fact]
    public async Task LastResult_And_ActiveFlag_TrackConfiguration()
    {
        using var device = await TestDeviceAsync();
        var invalidator = new CountingInvalidator();
        var coordinator = new SyncCoordinator(
            device.CreateEngine(new FakeFirestoreTransport()), device.Settings, invalidator);

        Assert.Null(coordinator.LastResult);
        Assert.True(await coordinator.IsActiveAsync());

        var s = await device.Settings.LoadAsync();
        s.SyncEnabled = false;
        await device.Settings.SaveAsync(s);

        Assert.False(await coordinator.IsActiveAsync());
        var result = await coordinator.SyncNowAsync();
        Assert.Equal(SyncStatus.Disabled, result.State);
        Assert.Equal(SyncStatus.Disabled, coordinator.LastResult?.State);
        Assert.Equal(0, invalidator.Count); // disabled cycles never touch the UI
    }

    private static async Task<TestDb> TestDeviceAsync()
    {
        var test = new TestDb();
        test.Initialize();
        await test.EnableSyncAsync();
        return test;
    }

    private static async Task CreateBookingAsync(TestDb test)
    {
        var service = new BookingService(test.Factory, test.Settings);
        await service.SaveBookingAsync(
            new Models.Guest { FullName = "Coordinator Guest", Phone = "09170001111" },
            new Models.Booking
            {
                CheckInDate = new DateTime(2026, 12, 10, 14, 0, 0),
                CheckOutDate = new DateTime(2026, 12, 11, 12, 0, 0),
                OvernightStayType = Models.OvernightStayType.Overnight,
                BaseAmount = 4000m
            },
            Array.Empty<BookingAddonInput>());
    }
}
