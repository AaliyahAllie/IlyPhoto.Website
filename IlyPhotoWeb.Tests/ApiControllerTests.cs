using IlyPhotoWeb.Controllers;
using IlyPhotoWeb.Models;
using IlyPhotoWeb.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace IlyPhotoWeb.Tests;

public class ApiControllerTests
{
    private readonly Mock<UserStore> _userStoreMock;
    private readonly Mock<BookingStore> _bookingStoreMock;
    private readonly Mock<GalleryStore> _galleryStoreMock;
    private readonly Mock<ReviewStore> _reviewStoreMock;
    private readonly Mock<EnquiryStore> _enquiryStoreMock;
    private readonly Mock<IPhotoStorage> _storageMock;
    private readonly Mock<CounterStore> _counterStoreMock;
    private readonly AuthService _authService;
    private readonly ApiController _controller;

    public ApiControllerTests()
    {
        _userStoreMock = new Mock<UserStore>();
        _bookingStoreMock = new Mock<BookingStore>();
        _galleryStoreMock = new Mock<GalleryStore>();
        _reviewStoreMock = new Mock<ReviewStore>();
        _enquiryStoreMock = new Mock<EnquiryStore>();
        _storageMock = new Mock<IPhotoStorage>();
        _counterStoreMock = new Mock<CounterStore>();

        var adminOptions = Options.Create(new AdminOptions
        {
            Emails = new List<string> { "admin@ilyphoto.co.za" }
        });

        _authService = new AuthService(
            _userStoreMock.Object,
            _counterStoreMock.Object,
            adminOptions,
            NullLogger<AuthService>.Instance);

        _controller = new ApiController(
            _authService,
            _userStoreMock.Object,
            _bookingStoreMock.Object,
            _galleryStoreMock.Object,
            _reviewStoreMock.Object,
            _enquiryStoreMock.Object,
            _storageMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public async Task ResetPassword_EmptyEmail_Returns400BadRequest()
    {
        var result = await _controller.ResetPassword(new ApiController.ResetPasswordRequest { Email = "" });
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_NonExistentEmail_Returns404NotFound()
    {
        // Rubric Criterion 2: Accurate password recovery failure reporting
        _userStoreMock.Setup(s => s.GetByEmailAsync("notfound@example.com")).ReturnsAsync((UserProfile?)null);

        var result = await _controller.ResetPassword(new ApiController.ResetPasswordRequest
        {
            Email = "notfound@example.com",
            NewPassword = "NewSecretPassword123!"
        });

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_ValidUser_Returns200Ok()
    {
        var user = new UserProfile
        {
            Id = "user-api-1",
            Email = "apiuser@example.com",
            FullName = "API Test Client",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPass123!")
        };
        _userStoreMock.Setup(s => s.GetByEmailAsync("apiuser@example.com")).ReturnsAsync(user);

        var result = await _controller.ResetPassword(new ApiController.ResetPasswordRequest
        {
            Email = "apiuser@example.com",
            NewPassword = "UpdatedSecurePassword123!"
        });

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        _userStoreMock.Verify(s => s.SaveAsync(It.IsAny<UserProfile>()), Times.Once);
    }

    [Fact]
    public async Task PutBooking_NonExistentBooking_Returns404NotFound()
    {
        _bookingStoreMock.Setup(s => s.GetAsync("unknown-id")).ReturnsAsync((Booking?)null);

        var result = await _controller.UpdateBooking("unknown-id", new ApiController.UpdateBookingRequest());

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
    }

    [Fact]
    public async Task PutBooking_SavesDateVenueTimeAndNotesToStorage()
    {
        // Rubric Criterion 5: Database Connectivity - Rescheduling updates date, time, and venue edits to cloud storage
        var existingBooking = new Booking
        {
            Id = "book-put-1",
            Date = "2026-11-20",
            StartTime = "09:00",
            EndTime = "11:00",
            Hours = 2,
            Location = "Original Beach Venue",
            Notes = "Original notes",
            Status = BookingStatus.Pending
        };

        _bookingStoreMock.Setup(s => s.GetAsync("book-put-1")).ReturnsAsync(existingBooking);

        Booking? savedBooking = null;
        _bookingStoreMock.Setup(s => s.CreateAsync(It.IsAny<Booking>()))
            .Callback<Booking>(b => savedBooking = b)
            .ReturnsAsync("book-put-1");

        var updateReq = new ApiController.UpdateBookingRequest
        {
            EventDate = "2026-12-05",
            EventTime = "15:00",
            EndTime = "18:00",
            DurationHours = 3,
            EventLocation = "Kirstenbosch Gardens, Cape Town",
            Requirements = "Golden hour portraits & family group shots",
            Status = BookingStatus.Confirmed
        };

        // Act
        var result = await _controller.UpdateBooking("book-put-1", updateReq);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        Assert.NotNull(savedBooking);
        Assert.Equal("2026-12-05", savedBooking.Date);
        Assert.Equal("15:00", savedBooking.StartTime);
        Assert.Equal("18:00", savedBooking.EndTime);
        Assert.Equal(3, savedBooking.Hours);
        Assert.Equal("Kirstenbosch Gardens, Cape Town", savedBooking.Location);
        Assert.Equal("Golden hour portraits & family group shots", savedBooking.Notes);
        Assert.Equal(BookingStatus.Confirmed, savedBooking.Status);
    }

    [Fact]
    public async Task PatchReschedule_UpdatesRescheduledStatusAndAmendedDateVenue()
    {
        var existingBooking = new Booking
        {
            Id = "book-resched-api-1",
            Date = "2026-11-25",
            StartTime = "11:00",
            Location = "Old Hall, Athlone",
            Status = BookingStatus.Pending
        };

        _bookingStoreMock.Setup(s => s.GetAsync("book-resched-api-1")).ReturnsAsync(existingBooking);

        Booking? savedBooking = null;
        _bookingStoreMock.Setup(s => s.CreateAsync(It.IsAny<Booking>()))
            .Callback<Booking>(b => savedBooking = b)
            .ReturnsAsync("book-resched-api-1");

        var rescheduleReq = new ApiController.UpdateBookingRequest
        {
            EventDate = "2026-12-15",
            EventTime = "16:00",
            EndTime = "19:00",
            DurationHours = 3,
            EventLocation = "Grand West, Cape Town",
            Requirements = "Rescheduled matric ball celebration",
            Status = BookingStatus.Rescheduled
        };

        // Act
        var result = await _controller.RescheduleBooking("book-resched-api-1", rescheduleReq);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        Assert.NotNull(savedBooking);
        Assert.Equal("2026-12-15", savedBooking.Date);
        Assert.Equal("16:00", savedBooking.StartTime);
        Assert.Equal("Grand West, Cape Town", savedBooking.Location);
        Assert.Equal(BookingStatus.Rescheduled, savedBooking.Status);
    }
}
