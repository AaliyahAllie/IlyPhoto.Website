using System.Globalization;
using IlyPhotoWeb.Models;
using IlyPhotoWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace IlyPhotoWeb.Controllers;

[ApiController]
[IgnoreAntiforgeryToken]
[Route("api")]
public class ApiController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly UserStore _userStore;
    private readonly BookingStore _bookingStore;
    private readonly InvoiceStore _invoiceStore;
    private readonly ServiceStore _serviceStore;
    private readonly GalleryStore _galleryStore;
    private readonly ReviewStore _reviewStore;
    private readonly EnquiryStore _enquiryStore;
    private readonly NotificationStore _notificationStore;
    private readonly IEmailService _emailService;
    private readonly IPhotoStorage _storage;

    public ApiController(
        AuthService auth,
        UserStore userStore,
        BookingStore bookingStore,
        InvoiceStore invoiceStore,
        ServiceStore serviceStore,
        GalleryStore galleryStore,
        ReviewStore reviewStore,
        EnquiryStore enquiryStore,
        NotificationStore notificationStore,
        IEmailService emailService,
        IPhotoStorage storage)
    {
        _auth = auth;
        _userStore = userStore;
        _bookingStore = bookingStore;
        _invoiceStore = invoiceStore;
        _serviceStore = serviceStore;
        _galleryStore = galleryStore;
        _reviewStore = reviewStore;
        _enquiryStore = enquiryStore;
        _notificationStore = notificationStore;
        _emailService = emailService;
        _storage = storage;
    }

    // -------------------------------------------------------------
    // Health Check
    // -------------------------------------------------------------
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "ok",
            timestamp = DateTime.UtcNow.ToString("o"),
            database = "Azure Table Storage",
            tableStorageConnected = true,
            platform = "IlyPhoto Cross-Platform Unified Backend (.NET)"
        });
    }

    // -------------------------------------------------------------
    // Authentication & Profile Endpoints
    // -------------------------------------------------------------
    public class RegisterRequest
    {
        public string? DisplayName { get; set; }
        public string? FullName { get; set; }
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string? Phone { get; set; }
        public string? Role { get; set; }
        public string? Username { get; set; }
    }

    [HttpPost("auth/register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        var name = !string.IsNullOrWhiteSpace(req.DisplayName) ? req.DisplayName : (req.FullName ?? "");
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(req.Password))
        {
            return BadRequest(new { error = "Email and password are required." });
        }

        var result = await _auth.RegisterAsync(name, email, req.Phone ?? "", req.Password);
        if (!result.Ok || result.Profile == null)
        {
            return BadRequest(new { error = result.Error ?? "Registration failed" });
        }

        var profile = result.Profile;
        return Ok(new
        {
            token = profile.Id,
            user = ToUserJson(profile)
        });
    }

    [HttpGet("auth/register")]
    public async Task<IActionResult> RegisterGet([FromQuery] string? email, [FromQuery] string? password, [FromQuery] string? fullName, [FromQuery] string? displayName, [FromQuery] string? phone, [FromQuery] string? role)
    {
        return await Register(new RegisterRequest
        {
            Email = email ?? "",
            Password = password ?? "",
            FullName = fullName ?? displayName,
            DisplayName = displayName ?? fullName,
            Phone = phone,
            Role = role
        });
    }

    public class LoginRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }

    [HttpPost("auth/login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        var result = await _auth.SignInAsync(email, req.Password ?? "");
        if (!result.Ok || result.Profile == null)
        {
            return Unauthorized(new { error = result.Error ?? "Invalid email or password." });
        }

        return Ok(new
        {
            token = result.Profile.Id,
            user = ToUserJson(result.Profile)
        });
    }

    [HttpGet("auth/login")]
    public async Task<IActionResult> LoginGet([FromQuery] string? email, [FromQuery] string? password)
    {
        return await Login(new LoginRequest { Email = email ?? "", Password = password ?? "" });
    }

    [HttpGet("auth/me")]
    public async Task<IActionResult> GetMe()
    {
        var user = await GetAuthenticatedUserAsync();
        if (user == null) return Unauthorized(new { error = "Not authenticated" });

        return Ok(new
        {
            user = ToUserJson(user)
        });
    }

    public class UpdateProfileRequest
    {
        public string? DisplayName { get; set; }
        public string? FullName { get; set; }
        public string? Username { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? Role { get; set; }
        public string? PhotoUrl { get; set; }
    }

    [HttpPut("auth/profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
    {
        var user = await GetAuthenticatedUserAsync();
        if (user == null) return Unauthorized(new { error = "Not authenticated" });

        if (!string.IsNullOrWhiteSpace(req.DisplayName)) user.FullName = req.DisplayName.Trim();
        else if (!string.IsNullOrWhiteSpace(req.FullName)) user.FullName = req.FullName.Trim();

        if (req.Phone != null) user.Phone = req.Phone.Trim();
        if (req.Address != null) user.Address = req.Address.Trim();
        if (req.PhotoUrl != null) user.PhotoUrl = req.PhotoUrl.Trim();

        await _userStore.SaveAsync(user);
        return Ok(new
        {
            user = ToUserJson(user)
        });
    }

    public class ResetPasswordRequest
    {
        public string Email { get; set; } = "";
        public string? NewPassword { get; set; }
    }

    [HttpPost("auth/reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        var result = await _auth.ResetPasswordAsync(req?.Email ?? "", req?.NewPassword);
        if (!result.Ok)
        {
            return BadRequest(new { ok = false, error = result.Error ?? "Password recovery failed." });
        }

        return Ok(new
        {
            ok = true,
            message = "Password recovery verified and notification dispatched to your registered email."
        });
    }

    // -------------------------------------------------------------
    // Bookings Endpoints
    // -------------------------------------------------------------
    [HttpGet("bookings")]
    public async Task<IActionResult> GetBookings()
    {
        var user = await GetAuthenticatedUserAsync();
        List<Booking> bookings;
        if (user != null && (user.Role == Roles.Admin || user.Role == Roles.Photographer))
        {
            bookings = await _bookingStore.ListAllAsync();
        }
        else if (user != null)
        {
            bookings = await _bookingStore.ListByClientAsync(user.ClientId);
            if (bookings.Count == 0)
            {
                bookings = await _bookingStore.ListByClientAsync(user.Id);
            }
            if (bookings.Count == 0 && !string.IsNullOrWhiteSpace(user.Email))
            {
                bookings = await _bookingStore.ListByClientAsync(user.Email);
            }
            if (bookings.Count == 0)
            {
                var all = await _bookingStore.ListAllAsync();
                bookings = all.Where(b =>
                    (!string.IsNullOrWhiteSpace(user.ClientId) && string.Equals(b.ClientId, user.ClientId, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(user.Id) && string.Equals(b.ClientId, user.Id, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(user.Email) && (string.Equals(b.ClientId, user.Email, StringComparison.OrdinalIgnoreCase) || string.Equals(b.ClientEmail, user.Email, StringComparison.OrdinalIgnoreCase))) ||
                    (!string.IsNullOrWhiteSpace(user.FullName) && string.Equals(b.ClientName, user.FullName, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }
        }
        else
        {
            bookings = await _bookingStore.ListAllAsync();
        }

        return Ok(bookings.Select(ToBookingJson));
    }

    public class CreateBookingRequest
    {
        public string? ClientId { get; set; }
        public string? ClientName { get; set; }
        public string? ClientEmail { get; set; }
        public string? ClientPhone { get; set; }
        public string? ServiceName { get; set; }
        public string? EventType { get; set; }
        public double? ServicePrice { get; set; }
        public int? DurationHours { get; set; }
        public string? EventDate { get; set; }
        public string? EventTime { get; set; }
        public string? EndTime { get; set; }
        public string? EventLocation { get; set; }
        public string? Requirements { get; set; }
        public string? Status { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? InvoiceStatus { get; set; }
    }

    [HttpPost("bookings")]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest req)
    {
        var eventDate = (req.EventDate ?? "").Trim();
        if (string.IsNullOrWhiteSpace(eventDate))
        {
            return BadRequest(new { error = "Event date is required." });
        }

        if (await _bookingStore.HasConflictOnDateAsync(eventDate))
        {
            return Conflict(new { error = "The selected date is unavailable or already has a confirmed booking." });
        }

        var user = await GetAuthenticatedUserAsync();
        var clientId = !string.IsNullOrWhiteSpace(req.ClientId) ? req.ClientId : (user?.ClientId ?? user?.Id ?? "CL-GUEST");
        var clientName = !string.IsNullOrWhiteSpace(req.ClientName) ? req.ClientName : (user?.FullName ?? "Client");
        var clientEmail = !string.IsNullOrWhiteSpace(req.ClientEmail) ? req.ClientEmail : (user?.Email ?? "");
        var clientPhone = !string.IsNullOrWhiteSpace(req.ClientPhone) ? req.ClientPhone : (user?.Phone ?? "");

        var startTime = !string.IsNullOrWhiteSpace(req.EventTime) ? req.EventTime.Trim() : "10:00";
        var hours = Math.Max(1, req.DurationHours ?? 2);
        var endTime = !string.IsNullOrWhiteSpace(req.EndTime)
            ? req.EndTime.Trim()
            : ComputeEndTime(startTime, hours);

        var booking = new Booking
        {
            Id = "book-" + Guid.NewGuid().ToString("N")[..8],
            ClientId = clientId,
            Uid = user?.Id ?? "",
            ClientName = clientName,
            ClientEmail = clientEmail,
            ClientPhone = clientPhone,
            EventType = !string.IsNullOrWhiteSpace(req.ServiceName) ? req.ServiceName : (req.EventType ?? "Photography Session"),
            Date = eventDate,
            StartTime = startTime,
            EndTime = endTime,
            Hours = hours,
            Location = !string.IsNullOrWhiteSpace(req.EventLocation) ? req.EventLocation.Trim() : "Cape Town",
            Notes = (req.Requirements ?? "").Trim(),
            Status = !string.IsNullOrWhiteSpace(req.Status) ? req.Status : BookingStatus.Pending,
            EstimatedTotal = req.ServicePrice ?? 1000,
            InvoiceNumber = req.InvoiceNumber ?? ("INV-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()),
            InvoiceStatus = req.InvoiceStatus ?? "Unpaid",
            CreatedAt = Timestamp.GetCurrentTimestamp()
        };

        await _bookingStore.CreateAsync(booking);

        var recipient = user ?? new UserProfile
        {
            ClientId = clientId,
            FullName = clientName,
            Email = clientEmail,
            Phone = clientPhone
        };
        await _emailService.SendBookingConfirmationEmailAsync(booking, recipient);

        return Ok(new { booking = ToBookingJson(booking) });
    }

    public class UpdateBookingRequest
    {
        public string? Status { get; set; }
        public string? EventDate { get; set; }
        public string? Date { get; set; }
        public string? EventTime { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public string? EventLocation { get; set; }
        public string? Location { get; set; }
        public string? ServiceName { get; set; }
        public string? EventType { get; set; }
        public double? ServicePrice { get; set; }
        public double? EstimatedTotal { get; set; }
        public int? DurationHours { get; set; }
        public int? Hours { get; set; }
        public string? Requirements { get; set; }
        public string? Notes { get; set; }
        public string? ClientPhone { get; set; }
        public string? InvoiceStatus { get; set; }
    }

    [HttpPut("bookings/{id}")]
    public Task<IActionResult> UpdateBooking(string id, [FromBody] UpdateBookingRequest req) =>
        ApplyBookingUpdateAsync(id, req);

    [HttpPatch("bookings/{id}/status")]
    public Task<IActionResult> UpdateStatus(string id, [FromBody] UpdateBookingRequest req) =>
        ApplyBookingUpdateAsync(id, req);

    private async Task<IActionResult> ApplyBookingUpdateAsync(string id, UpdateBookingRequest req)
    {
        var booking = await _bookingStore.GetAsync(id);
        if (booking == null) return NotFound(new { error = "Booking not found" });

        var newDate = !string.IsNullOrWhiteSpace(req.EventDate) ? req.EventDate.Trim()
            : (!string.IsNullOrWhiteSpace(req.Date) ? req.Date.Trim() : null);

        var newStart = !string.IsNullOrWhiteSpace(req.EventTime) ? req.EventTime.Trim()
            : (!string.IsNullOrWhiteSpace(req.StartTime) ? req.StartTime.Trim() : null);

        var newEnd = !string.IsNullOrWhiteSpace(req.EndTime) ? req.EndTime.Trim() : null;

        var newLocation = !string.IsNullOrWhiteSpace(req.EventLocation) ? req.EventLocation.Trim()
            : (!string.IsNullOrWhiteSpace(req.Location) ? req.Location.Trim() : null);

        var newService = !string.IsNullOrWhiteSpace(req.ServiceName) ? req.ServiceName.Trim()
            : (!string.IsNullOrWhiteSpace(req.EventType) ? req.EventType.Trim() : null);

        var newHours = req.DurationHours ?? req.Hours;
        var newPrice = req.ServicePrice ?? req.EstimatedTotal;
        var newNotes = req.Requirements ?? req.Notes;

        bool detailsAmended = false;

        if (!string.IsNullOrWhiteSpace(newDate) && newDate != booking.Date)
        {
            if (await _bookingStore.HasConflictOnDateAsync(newDate, booking.Id))
            {
                return Conflict(new { error = "The target reschedule date is unavailable or already booked." });
            }
            booking.Date = newDate;
            detailsAmended = true;
        }

        if (!string.IsNullOrWhiteSpace(newStart) && newStart != booking.StartTime)
        {
            booking.StartTime = newStart;
            detailsAmended = true;
        }

        if (newHours.HasValue && newHours.Value > 0)
        {
            booking.Hours = newHours.Value;
            detailsAmended = true;
        }

        if (!string.IsNullOrWhiteSpace(newEnd))
        {
            booking.EndTime = newEnd;
            var computed = Format.HoursBetween(booking.StartTime, booking.EndTime);
            if (computed.HasValue && !newHours.HasValue) booking.Hours = computed.Value;
            detailsAmended = true;
        }
        else if (detailsAmended)
        {
            booking.EndTime = ComputeEndTime(booking.StartTime, booking.Hours);
        }

        if (!string.IsNullOrWhiteSpace(newLocation))
        {
            booking.Location = newLocation;
            detailsAmended = true;
        }

        if (!string.IsNullOrWhiteSpace(newService))
        {
            booking.EventType = newService;
            detailsAmended = true;
        }

        if (newPrice.HasValue && newPrice.Value >= 0)
        {
            booking.EstimatedTotal = newPrice.Value;
            detailsAmended = true;
        }

        if (newNotes != null)
        {
            booking.Notes = newNotes.Trim();
            detailsAmended = true;
        }

        if (!string.IsNullOrWhiteSpace(req.ClientPhone))
        {
            booking.ClientPhone = req.ClientPhone.Trim();
        }

        if (!string.IsNullOrWhiteSpace(req.InvoiceStatus))
        {
            booking.InvoiceStatus = req.InvoiceStatus.Trim();
        }

        if (!string.IsNullOrWhiteSpace(req.Status))
        {
            booking.Status = req.Status.Trim();
        }

        // Auto-generate invoice if booking is confirmed and no invoice record exists yet
        if (string.Equals(booking.Status, BookingStatus.Confirmed, StringComparison.OrdinalIgnoreCase))
        {
            var existingInvoices = await _invoiceStore.ListByClientAsync(booking.ClientId);
            var linked = existingInvoices.FirstOrDefault(i => i.BookingId == booking.Id);
            if (linked == null)
            {
                var inv = await _invoiceStore.CreateAsync(new Invoice
                {
                    ClientId = booking.ClientId,
                    BookingId = booking.Id,
                    BookingLabel = booking.Label,
                    IssueDate = Format.TodayIso(),
                    DueDate = booking.Date,
                    Service = $"{booking.EventType} ({booking.Hours}h coverage at {booking.Location})",
                    Quantity = 1,
                    Price = booking.EstimatedTotal > 0 ? booking.EstimatedTotal : 1000,
                    Status = InvoiceStatus.Unpaid,
                    EmailSent = true
                });
                booking.InvoiceNumber = inv.InvoiceNo;
                booking.InvoiceStatus = inv.Status;
            }
        }

        await _bookingStore.UpdateBookingAsync(booking);

        var client = await _userStore.GetByClientIdAsync(booking.ClientId)
            ?? (!string.IsNullOrWhiteSpace(booking.ClientEmail) ? await _userStore.GetByEmailAsync(booking.ClientEmail) : null)
            ?? new UserProfile
            {
                ClientId = booking.ClientId,
                FullName = booking.ClientName,
                Email = booking.ClientEmail,
                Phone = booking.ClientPhone
            };

        if (detailsAmended || string.Equals(booking.Status, BookingStatus.Rescheduled, StringComparison.OrdinalIgnoreCase))
        {
            await _emailService.SendBookingRescheduledEmailAsync(booking, client);
        }
        else
        {
            await _emailService.SendBookingConfirmationEmailAsync(booking, client);
        }

        return Ok(new { booking = ToBookingJson(booking) });
    }

    public class InvoiceStatusRequest
    {
        public string InvoiceStatus { get; set; } = "";
    }

    [HttpPatch("bookings/{id}/invoice")]
    public async Task<IActionResult> UpdateInvoice(string id, [FromBody] InvoiceStatusRequest req)
    {
        var booking = await _bookingStore.GetAsync(id);
        if (booking == null) return NotFound(new { error = "Booking not found" });

        booking.InvoiceStatus = req.InvoiceStatus;
        await _bookingStore.UpdateBookingAsync(booking);
        return Ok(new { booking = ToBookingJson(booking) });
    }

    // -------------------------------------------------------------
    // Invoices, Services & Notifications Endpoints
    // -------------------------------------------------------------
    [HttpGet("invoices")]
    public async Task<IActionResult> GetInvoices([FromQuery] string? clientId = null)
    {
        var user = await GetAuthenticatedUserAsync();
        List<Invoice> list;
        if (!string.IsNullOrWhiteSpace(clientId))
        {
            list = await _invoiceStore.ListByClientAsync(clientId);
        }
        else if (user != null && user.Role != Roles.Admin && user.Role != Roles.Photographer)
        {
            list = await _invoiceStore.ListByClientAsync(user.ClientId);
        }
        else
        {
            list = await _invoiceStore.ListAllAsync();
        }
        return Ok(list);
    }

    [HttpGet("services")]
    public async Task<IActionResult> GetServices()
    {
        var list = await _serviceStore.ListAsync();
        return Ok(list);
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications([FromQuery] string? clientId = null, [FromQuery] string? email = null)
    {
        var user = await GetAuthenticatedUserAsync();
        var cid = !string.IsNullOrWhiteSpace(clientId) ? clientId : (user?.ClientId ?? "");
        var em = !string.IsNullOrWhiteSpace(email) ? email : (user?.Email ?? "");
        if (!string.IsNullOrWhiteSpace(cid) || !string.IsNullOrWhiteSpace(em))
        {
            var items = await _notificationStore.ListByClientAsync(cid, em);
            return Ok(items);
        }
        return Ok(await _notificationStore.ListAllAsync());
    }

    // -------------------------------------------------------------
    // Photos & Portfolio Endpoints
    // -------------------------------------------------------------
    [HttpGet("photos")]
    public async Task<IActionResult> GetPhotos()
    {
        var photos = new List<object>();
        try
        {
            var galleries = await _galleryStore.ListAllAsync();
            foreach (var g in galleries)
            {
                foreach (var path in g.PhotoPaths)
                {
                    var url = _storage.GetReadUrl(path, TimeSpan.FromHours(24));
                    photos.Add(new
                    {
                        id = path,
                        title = g.Title,
                        description = $"{g.Title} photo",
                        category = "Portfolio",
                        tags = new[] { "gallery", "portfolio" },
                        imageUrl = url,
                        storagePath = path,
                        clientId = g.ClientId,
                        bookingId = g.BookingId,
                        photographerId = "ADMIN",
                        createdAt = g.CreatedAt.ToString()
                    });
                }
            }
        }
        catch { }

        return Ok(photos);
    }

    public class PhotoRequest
    {
        public string? Title { get; set; }
        public string? Category { get; set; }
        public string? ImageUrl { get; set; }
        public string? StoragePath { get; set; }
        public string? ClientId { get; set; }
        public string? BookingId { get; set; }
    }

    [HttpPost("photos")]
    public IActionResult CreatePhoto([FromBody] PhotoRequest req)
    {
        return Ok(new
        {
            photo = new
            {
                id = Guid.NewGuid().ToString("N")[..8],
                title = req.Title ?? "Untitled",
                category = req.Category ?? "General",
                imageUrl = req.ImageUrl ?? "",
                storagePath = req.StoragePath ?? "",
                clientId = req.ClientId ?? "",
                bookingId = req.BookingId ?? "",
                createdAt = DateTime.UtcNow.ToString("o")
            }
        });
    }

    [HttpDelete("photos/{id}")]
    public IActionResult DeletePhoto(string id)
    {
        return Ok(new { ok = true });
    }

    // -------------------------------------------------------------
    // Enquiries Endpoints
    // -------------------------------------------------------------
    public class EnquiryRequest
    {
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Message { get; set; }
    }

    [HttpGet("enquiries")]
    public async Task<IActionResult> GetEnquiries()
    {
        var list = await _enquiryStore.ListAllAsync();
        return Ok(list.Select(e => new
        {
            id = e.Id,
            fullName = e.FullName,
            email = e.Email,
            phone = e.Phone,
            message = e.Message,
            createdAt = e.CreatedAt.ToString()
        }));
    }

    [HttpPost("enquiries")]
    public async Task<IActionResult> CreateEnquiry([FromBody] EnquiryRequest req)
    {
        if (string.IsNullOrWhiteSpace(req?.FullName) || string.IsNullOrWhiteSpace(req?.Email) || string.IsNullOrWhiteSpace(req?.Message))
        {
            return BadRequest(new { error = "FullName, Email, and Message are required." });
        }

        var enquiry = new ContactEnquiry
        {
            Id = "enq-" + Guid.NewGuid().ToString("N")[..8],
            FullName = req.FullName.Trim(),
            Email = req.Email.Trim(),
            Phone = (req.Phone ?? "").Trim(),
            Message = req.Message.Trim(),
            CreatedAt = Timestamp.GetCurrentTimestamp()
        };
        await _enquiryStore.CreateAsync(enquiry);
        return Ok(new
        {
            enquiry = new
            {
                id = enquiry.Id,
                fullName = enquiry.FullName,
                email = enquiry.Email,
                phone = enquiry.Phone,
                message = enquiry.Message,
                createdAt = enquiry.CreatedAt.ToString()
            }
        });
    }

    // -------------------------------------------------------------
    // Reviews Endpoints
    // -------------------------------------------------------------
    public class ReviewRequest
    {
        public string? ClientId { get; set; }
        public string? ClientName { get; set; }
        public int? Rating { get; set; }
        public string? Text { get; set; }
    }

    [HttpGet("reviews")]
    public async Task<IActionResult> GetReviews()
    {
        var list = await _reviewStore.ListApprovedAsync();
        return Ok(list.Select(r => new
        {
            id = r.Id,
            clientId = r.ClientId,
            clientName = r.ClientName,
            authorName = r.ClientName,
            rating = r.Rating,
            text = r.Text,
            approved = r.Approved,
            createdAt = r.CreatedAt.ToString()
        }));
    }

    [HttpPost("reviews")]
    public async Task<IActionResult> CreateReview([FromBody] ReviewRequest req)
    {
        if (string.IsNullOrWhiteSpace(req?.Text))
        {
            return BadRequest(new { error = "Review text is required." });
        }

        var user = await GetAuthenticatedUserAsync();
        var review = new Review
        {
            Id = "rev-" + Guid.NewGuid().ToString("N")[..8],
            ClientId = req.ClientId ?? user?.ClientId ?? "GUEST",
            ClientName = req.ClientName ?? user?.FullName ?? "Client",
            Rating = Math.Clamp(req.Rating ?? 5, 1, 5),
            Text = req.Text.Trim(),
            Approved = true,
            CreatedAt = Timestamp.GetCurrentTimestamp()
        };
        await _reviewStore.CreateAsync(review);
        return Ok(new
        {
            review = new
            {
                id = review.Id,
                clientId = review.ClientId,
                clientName = review.ClientName,
                rating = review.Rating,
                text = review.Text,
                approved = review.Approved,
                createdAt = review.CreatedAt.ToString()
            }
        });
    }

    // -------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------
    private static string ComputeEndTime(string startTime, int hours)
    {
        if (Format.TryParseTime(startTime, out var ts))
        {
            var end = ts.Add(TimeSpan.FromHours(Math.Max(1, hours)));
            if (end.TotalHours >= 24) return "23:59";
            return $"{(int)end.TotalHours:D2}:{end.Minutes:D2}";
        }
        return "12:00";
    }

    private static object ToUserJson(UserProfile p) => new
    {
        uid = p.Id,
        displayName = p.FullName,
        fullName = p.FullName,
        email = p.Email,
        phone = p.Phone,
        address = p.Address,
        photoUrl = p.PhotoUrl,
        role = p.Role.ToLowerInvariant(),
        clientId = p.ClientId
    };

    private static object ToBookingJson(Booking b) => new
    {
        id = b.Id,
        clientId = b.ClientId,
        clientName = b.ClientName,
        clientEmail = b.ClientEmail,
        clientPhone = b.ClientPhone,
        serviceName = b.EventType,
        servicePrice = Convert.ToInt32(b.EstimatedTotal),
        durationHours = b.Hours,
        eventDate = b.Date,
        eventTime = b.StartTime,
        endTime = b.EndTime,
        eventLocation = b.Location,
        requirements = b.Notes,
        status = b.Status,
        invoiceNumber = b.InvoiceNumber,
        invoiceStatus = b.InvoiceStatus,
        createdAt = b.CreatedAt.ToString()
    };

    private async Task<UserProfile?> GetAuthenticatedUserAsync()
    {
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader))
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                if (!string.IsNullOrEmpty(email)) return await _userStore.GetByEmailAsync(email);
            }
            return null;
        }

        var token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authHeader["Bearer ".Length..].Trim()
            : authHeader.Trim();

        if (string.IsNullOrWhiteSpace(token)) return null;

        var user = await _userStore.GetAsync(token);
        if (user != null) return user;

        return await _userStore.GetByEmailAsync(token);
    }
}
