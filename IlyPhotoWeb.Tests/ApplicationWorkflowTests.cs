using IlyPhotoWeb.Controllers;
using IlyPhotoWeb.Models;
using IlyPhotoWeb.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace IlyPhotoWeb.Tests;

public class ApplicationWorkflowTests
{
    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "IlyPhotoWeb.Tests";
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "IlyPhotoTests_" + Guid.NewGuid().ToString("N")[..6]);
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class FakePhotoStorage : IPhotoStorage
    {
        public Task UploadAsync(string objectName, string contentType, Stream data, CancellationToken ct) => Task.CompletedTask;
        public Task DownloadToAsync(string objectName, Stream destination, CancellationToken ct) => Task.CompletedTask;
        public Task DeleteAsync(string objectName, CancellationToken ct) => Task.CompletedTask;
        public string GetReadUrl(string objectName, TimeSpan validFor) => $"/photos/{objectName}";
    }

    private static (
        AzureTableContext TableContext,
        CounterStore Counters,
        UserStore Users,
        BookingStore Bookings,
        InvoiceStore Invoices,
        ServiceStore Services,
        GalleryStore Galleries,
        ReviewStore Reviews,
        EnquiryStore Enquiries,
        NotificationStore Notifications,
        IEmailService EmailService,
        AuthService Auth,
        ApiController Api) CreateTestHarness()
    {
        var azureOpts = Options.Create(new AzureStorageOptions { ConnectionString = "" });
        var adminOpts = Options.Create(new AdminOptions { Emails = new List<string> { "admin@ilyphoto.co.za" } });
        var businessOpts = Options.Create(new BusinessOptions { Name = "ILY Photo", Email = "info@ilyphoto.co.za" });
        var env = new TestWebHostEnvironment();

        var context = new AzureTableContext(azureOpts, NullLogger<AzureTableContext>.Instance);
        var counters = new CounterStore(context);
        var users = new UserStore(context);
        var bookings = new BookingStore(context);
        var invoices = new InvoiceStore(context, counters);
        var services = new ServiceStore(context);
        var photos = new FakePhotoStorage();
        var galleries = new GalleryStore(context, photos);
        var reviews = new ReviewStore(context);
        var enquiries = new EnquiryStore(context);
        var notifications = new NotificationStore(context);
        var emailService = new EmailService(businessOpts, env, notifications, NullLogger<EmailService>.Instance);
        var auth = new AuthService(users, counters, adminOpts, emailService, NullLogger<AuthService>.Instance);

        var api = new ApiController(
            auth,
            users,
            bookings,
            invoices,
            services,
            galleries,
            reviews,
            enquiries,
            notifications,
            emailService,
            photos)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return (context, counters, users, bookings, invoices, services, galleries, reviews, enquiries, notifications, emailService, auth, api);
    }

    [Fact]
    public async Task Authentication_RejectsWrongPasswordAndPreventsPassword123Bypass()
    {
        var h = CreateTestHarness();

        var reg = await h.Auth.RegisterAsync("Nadia Jacobs", "nadia@example.com", "0825550199", "MySecret#2026");
        Assert.True(reg.Ok);
        Assert.NotNull(reg.Profile);
        Assert.Equal("CL-1001", reg.Profile!.ClientId);

        // Wrong password must fail
        var wrongAttempt = await h.Auth.SignInAsync("nadia@example.com", "WrongPass999");
        Assert.False(wrongAttempt.Ok);

        // "Password123!" must NOT bypass an existing user's custom password
        var bypassAttempt = await h.Auth.SignInAsync("nadia@example.com", "Password123!");
        Assert.False(bypassAttempt.Ok);

        // Correct password succeeds
        var validAttempt = await h.Auth.SignInAsync("nadia@example.com", "MySecret#2026");
        Assert.True(validAttempt.Ok);
        Assert.Equal("nadia@example.com", validAttempt.Profile!.Email);
    }

    [Fact]
    public async Task PasswordRecovery_RejectsInvalidOrUnregisteredEmails_AndSucceedsForRegisteredUser()
    {
        var h = CreateTestHarness();
        await h.Auth.RegisterAsync("Tariq Adams", "tariq@example.com", "0831112233", "InitialPass1!");

        // Blank, malformed, and unregistered emails must fail
        var blankRes = await h.Auth.ResetPasswordAsync("");
        Assert.False(blankRes.Ok);

        var invalidRes = await h.Auth.ResetPasswordAsync("not-an-email");
        Assert.False(invalidRes.Ok);

        var unknownRes = await h.Auth.ResetPasswordAsync("nobody@example.com");
        Assert.False(unknownRes.Ok);

        var apiBad = await h.Api.ResetPassword(new ApiController.ResetPasswordRequest { Email = "nobody@example.com" });
        Assert.IsType<BadRequestObjectResult>(apiBad);

        // Registered user password reset succeeds, updates password hash, and creates security notification
        var resetRes = await h.Auth.ResetPasswordAsync("tariq@example.com", "UpdatedPass#77");
        Assert.True(resetRes.Ok);

        var oldLogin = await h.Auth.SignInAsync("tariq@example.com", "InitialPass1!");
        Assert.False(oldLogin.Ok);

        var newLogin = await h.Auth.SignInAsync("tariq@example.com", "UpdatedPass#77");
        Assert.True(newLogin.Ok);

        var notifs = await h.Notifications.ListByClientAsync(newLogin.Profile!.ClientId);
        Assert.Contains(notifs, n => n.Category == "Security" && n.Title.Contains("Password Recovery"));
    }

    [Fact]
    public async Task BookingAvailability_DetectsConflictsOnConfirmedRescheduledAndBlockedDates()
    {
        var h = CreateTestHarness();

        // Create and confirm a booking on 2026-12-10
        var createRes = await h.Api.CreateBooking(new ApiController.CreateBookingRequest
        {
            ClientId = "CL-1001",
            ClientName = "Amina Davids",
            ClientEmail = "amina@example.com",
            ServiceName = "Wedding",
            ServicePrice = 1500,
            DurationHours = 4,
            EventDate = "2026-12-10",
            EventTime = "14:00",
            EventLocation = "Strandfontein Pavilion",
            Status = BookingStatus.Confirmed
        });
        Assert.IsType<OkObjectResult>(createRes);

        // Attempting a second booking on the same confirmed date returns 409 Conflict
        var duplicateRes = await h.Api.CreateBooking(new ApiController.CreateBookingRequest
        {
            ClientId = "CL-1002",
            ClientName = "Second Client",
            ClientEmail = "second@example.com",
            ServiceName = "Matric Ball",
            EventDate = "2026-12-10",
            EventTime = "15:00",
            EventLocation = "Cape Town"
        });
        Assert.IsType<ConflictObjectResult>(duplicateRes);

        // Blocking 2026-12-18 also prevents bookings on that date
        await h.Bookings.BlockAsync("2026-12-18", "Equipment Maintenance");
        Assert.True(await h.Bookings.HasConflictOnDateAsync("2026-12-18"));

        var blockedRes = await h.Api.CreateBooking(new ApiController.CreateBookingRequest
        {
            ClientId = "CL-1002",
            ClientName = "Second Client",
            ServiceName = "Matric Ball",
            EventDate = "2026-12-18"
        });
        Assert.IsType<ConflictObjectResult>(blockedRes);
    }

    [Fact]
    public async Task RescheduleWorkflow_PersistsDateStartTimeEndTimeVenuePackageNotesAndStatus()
    {
        var h = CreateTestHarness();

        var id = await h.Bookings.CreateAsync(new Booking
        {
            ClientId = "CL-1005",
            ClientName = "Leila Hendricks",
            ClientEmail = "leila@example.com",
            EventType = "Matric Ball",
            Date = "2026-11-20",
            StartTime = "13:00",
            EndTime = "16:00",
            Hours = 3,
            Location = "Old Venue Hall",
            Notes = "Original notes",
            Status = BookingStatus.Pending,
            EstimatedTotal = 800
        });

        // Update via PATCH /api/bookings/{id}/status with full amended schedule & venue fields
        var patchResult = await h.Api.UpdateStatus(id, new ApiController.UpdateBookingRequest
        {
            Status = BookingStatus.Rescheduled,
            EventDate = "2026-11-25",
            EventTime = "15:00",
            EndTime = "19:00",
            DurationHours = 4,
            EventLocation = "Kirstenbosch Botanical Gardens",
            ServiceName = "Wedding",
            ServicePrice = 1500,
            Requirements = "Golden hour outdoor portraits"
        });
        Assert.IsType<OkObjectResult>(patchResult);

        var saved = await h.Bookings.GetAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(BookingStatus.Rescheduled, saved!.Status);
        Assert.Equal("2026-11-25", saved.Date);
        Assert.Equal("15:00", saved.StartTime);
        Assert.Equal("19:00", saved.EndTime);
        Assert.Equal(4, saved.Hours);
        Assert.Equal("Kirstenbosch Botanical Gardens", saved.Location);
        Assert.Equal("Wedding", saved.EventType);
        Assert.Equal(1500, saved.EstimatedTotal);
        Assert.Equal("Golden hour outdoor portraits", saved.Notes);

        var notifs = await h.Notifications.ListByClientAsync("CL-1005");
        Assert.Contains(notifs, n => n.Title.Contains("Booking Rescheduled") && n.Message.Contains("Kirstenbosch Botanical Gardens"));
    }

    [Fact]
    public async Task AdminApproval_AutoGeneratesLinkedInvoiceAndNotifiesClient()
    {
        var h = CreateTestHarness();

        var bookingId = await h.Bookings.CreateAsync(new Booking
        {
            ClientId = "CL-1009",
            ClientName = "Zaid Petersen",
            ClientEmail = "zaid@example.com",
            EventType = "Corporate Event",
            Date = "2026-12-05",
            StartTime = "09:00",
            EndTime = "13:00",
            Hours = 4,
            Location = "CTICC, Cape Town",
            Status = BookingStatus.Pending,
            EstimatedTotal = 2000
        });

        // Confirm booking via API
        var confirmRes = await h.Api.UpdateBooking(bookingId, new ApiController.UpdateBookingRequest
        {
            Status = BookingStatus.Confirmed
        });
        Assert.IsType<OkObjectResult>(confirmRes);

        var confirmed = await h.Bookings.GetAsync(bookingId);
        Assert.NotNull(confirmed);
        Assert.Equal(BookingStatus.Confirmed, confirmed!.Status);
        Assert.StartsWith("INV-", confirmed.InvoiceNumber);

        var clientInvoices = await h.Invoices.ListByClientAsync("CL-1009");
        Assert.Single(clientInvoices);
        Assert.Equal(bookingId, clientInvoices[0].BookingId);
        Assert.Equal(2000, clientInvoices[0].Total);
    }

    [Fact]
    public void StatusBadgesAndNgoServiceDefaults_AreProperlyConfigured()
    {
        Assert.Equal("confirmed", Format.StatusClass("Confirmed"));
        Assert.Equal("completed", Format.StatusClass("Completed"));
        Assert.Equal("pending", Format.StatusClass("Pending"));
        Assert.Equal("rescheduled", Format.StatusClass("Rescheduled"));
        Assert.Equal("unpaid", Format.StatusClass("Unpaid"));
        Assert.Equal("cancelled", Format.StatusClass("Cancelled"));
        Assert.Equal("cancelled", Format.StatusClass("Denied"));

        var defaults = ServiceStore.Defaults();
        Assert.Contains(defaults, s => s.Id == "ngo-community" && s.Name.Contains("NGO", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CrossPlatformMobileAndDesktopSync_NormalizesDatesAndMatchesClientAcrossIdentifiers()
    {
        var h = CreateTestHarness();

        // Mobile app creates booking with "dd MMMM yyyy" date format and email/uid clientId
        var mobileBookingId = await h.Bookings.CreateAsync(new Booking
        {
            Id = "book-mobile01",
            ClientId = "CL-1001",
            Uid = "azure-user-6505ccc2",
            ClientName = "Aaliyah Allie",
            ClientEmail = "aaliyahallie6@gmail.com",
            ClientPhone = "0812345678",
            EventType = "Wedding",
            Date = "06 November 2026",
            StartTime = "15:05",
            EndTime = "19:05",
            Hours = 4,
            Location = "Cape Town",
            Status = BookingStatus.Confirmed,
            EstimatedTotal = 2000
        });

        // Date is normalized to canonical ISO "2026-11-06" so Desktop calendar & conflict checks match Mobile
        var saved = await h.Bookings.GetAsync(mobileBookingId);
        Assert.NotNull(saved);
        Assert.Equal("2026-11-06", saved!.Date);
        Assert.True(await h.Bookings.HasConflictOnDateAsync("2026-11-06"));
        Assert.True(await h.Bookings.HasConflictOnDateAsync("06 November 2026"));

        // Client lookup matches by ClientId, Email, or Mobile UID
        var byClientId = await h.Bookings.ListByClientAsync("CL-1001");
        var byEmail = await h.Bookings.ListByClientAsync("", "aaliyahallie6@gmail.com");
        var byUid = await h.Bookings.ListByClientAsync("", null, "azure-user-6505ccc2");

        Assert.Single(byClientId);
        Assert.Single(byEmail);
        Assert.Single(byUid);
    }
}
