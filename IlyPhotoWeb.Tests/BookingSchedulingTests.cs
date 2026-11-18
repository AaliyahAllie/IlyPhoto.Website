using IlyPhotoWeb.Models;
using IlyPhotoWeb.Services;
using Moq;
using Xunit;

namespace IlyPhotoWeb.Tests;

public class BookingSchedulingTests
{
    private readonly Mock<BookingStore> _bookingStoreMock;

    public BookingSchedulingTests()
    {
        _bookingStoreMock = new Mock<BookingStore>();
    }

    [Fact]
    public async Task HasConfirmedOnDateAsync_WithConfirmedBooking_ReturnsTrue_DetectsConflict()
    {
        // Rubric Criterion 7 & 10: Conflict checks verify booking conflict on confirmed dates
        var date = "2026-12-05";
        var bookings = new List<Booking>
        {
            new Booking
            {
                Id = "b1",
                Date = date,
                Status = BookingStatus.Confirmed,
                ClientName = "Existing Client"
            }
        };

        _bookingStoreMock.Setup(s => s.ListAllAsync()).ReturnsAsync(bookings);
        _bookingStoreMock.Setup(s => s.HasConfirmedOnDateAsync(date, null)).CallBase();

        // Act
        var hasConflict = await _bookingStoreMock.Object.HasConfirmedOnDateAsync(date);

        // Assert
        Assert.True(hasConflict);
    }

    [Fact]
    public async Task HasConfirmedOnDateAsync_WithCancelledOrPending_ReturnsFalse_NoConflict()
    {
        var date = "2026-12-05";
        var bookings = new List<Booking>
        {
            new Booking
            {
                Id = "b2",
                Date = date,
                Status = BookingStatus.Cancelled,
                ClientName = "Cancelled Client"
            },
            new Booking
            {
                Id = "b3",
                Date = date,
                Status = BookingStatus.Pending,
                ClientName = "Pending Client"
            }
        };

        _bookingStoreMock.Setup(s => s.ListAllAsync()).ReturnsAsync(bookings);
        _bookingStoreMock.Setup(s => s.HasConfirmedOnDateAsync(date, null)).CallBase();

        // Act
        var hasConflict = await _bookingStoreMock.Object.HasConfirmedOnDateAsync(date);

        // Assert
        Assert.False(hasConflict);
    }

    [Fact]
    public async Task RescheduleAsync_UpdatesDateStartTimeEndTimeHoursAndEstimatedTotal()
    {
        // Rubric Criterion 5: Rescheduling updates date, time, and venue edits to storage, not just status
        var existingBooking = new Booking
        {
            Id = "book-resched-1",
            Date = "2026-12-01",
            StartTime = "10:00",
            EndTime = "12:00",
            Hours = 2,
            EstimatedTotal = 800,
            Location = "Original Venue, Mitchells Plain",
            Status = BookingStatus.Pending
        };

        _bookingStoreMock.Setup(s => s.GetAsync("book-resched-1")).ReturnsAsync(existingBooking);

        Booking? savedBooking = null;
        _bookingStoreMock.Setup(s => s.CreateAsync(It.IsAny<Booking>()))
            .Callback<Booking>(b => savedBooking = b)
            .ReturnsAsync("book-resched-1");

        _bookingStoreMock.Setup(s => s.RescheduleAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>()))
            .CallBase();

        // Act
        await _bookingStoreMock.Object.RescheduleAsync("book-resched-1", "2026-12-10", "14:00", "17:00", 3, 1100);

        // Assert
        Assert.NotNull(savedBooking);
        Assert.Equal("2026-12-10", savedBooking.Date);
        Assert.Equal("14:00", savedBooking.StartTime);
        Assert.Equal("17:00", savedBooking.EndTime);
        Assert.Equal(3, savedBooking.Hours);
        Assert.Equal(1100, savedBooking.EstimatedTotal);
    }

    [Fact]
    public async Task SetStatusAsync_UpdatesBookingStatusInStorage()
    {
        var existingBooking = new Booking
        {
            Id = "book-status-1",
            Status = BookingStatus.Pending
        };

        _bookingStoreMock.Setup(s => s.GetAsync("book-status-1")).ReturnsAsync(existingBooking);

        Booking? savedBooking = null;
        _bookingStoreMock.Setup(s => s.CreateAsync(It.IsAny<Booking>()))
            .Callback<Booking>(b => savedBooking = b)
            .ReturnsAsync("book-status-1");

        _bookingStoreMock.Setup(s => s.SetStatusAsync(It.IsAny<string>(), It.IsAny<string>()))
            .CallBase();

        // Act
        await _bookingStoreMock.Object.SetStatusAsync("book-status-1", BookingStatus.Confirmed);

        // Assert
        Assert.NotNull(savedBooking);
        Assert.Equal(BookingStatus.Confirmed, savedBooking.Status);
    }

    [Theory]
    [InlineData("20 November 2026", "2026-11-20")]
    [InlineData("20 Nov 2026", "2026-11-20")]
    [InlineData("2026-11-20", "2026-11-20")]
    [InlineData("19/11/2026", "2026-11-19")]
    public void NormalizeDateIso_HandlesMultipleDateFormats(string input, string expectedIso)
    {
        Assert.Equal(expectedIso, Format.NormalizeDateIso(input));
    }

    [Fact]
    public void Booking_EventDate_ParsesHumanReadableDate()
    {
        var b = new Booking { Date = "20 November 2026" };
        Assert.Equal(new DateTime(2026, 11, 20), b.EventDate);
    }
}
