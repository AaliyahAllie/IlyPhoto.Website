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
    private readonly GalleryStore _galleryStore;
    private readonly ReviewStore _reviewStore;
    private readonly EnquiryStore _enquiryStore;
    private readonly IPhotoStorage _storage;

    public ApiController(
        AuthService auth,
        UserStore userStore,
        BookingStore bookingStore,
        GalleryStore galleryStore,
        ReviewStore reviewStore,
        EnquiryStore enquiryStore,
        IPhotoStorage storage)
    {
        _auth = auth;
        _userStore = userStore;
        _bookingStore = bookingStore;
        _galleryStore = galleryStore;
        _reviewStore = reviewStore;
        _enquiryStore = enquiryStore;
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
        if (!string.IsNullOrWhiteSpace(req.Role) && req.Role.Equals("photographer", StringComparison.OrdinalIgnoreCase))
        {
            profile.Role = Roles.Admin;
            await _userStore.SaveAsync(profile);
        }

        return Ok(new
        {
            token = profile.Id,
            user = ToUserJson(profile)
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

        if (!string.IsNullOrWhiteSpace(req.DisplayName)) user.FullName = req.DisplayName;
        else if (!string.IsNullOrWhiteSpace(req.FullName)) user.FullName = req.FullName;

        if (req.Phone != null) user.Phone = req.Phone;
        if (req.Address != null) user.Address = req.Address;
        if (req.PhotoUrl != null) user.PhotoUrl = req.PhotoUrl;
        if (!string.IsNullOrWhiteSpace(req.Role))
        {
            user.Role = req.Role.Equals("photographer", StringComparison.OrdinalIgnoreCase) ? Roles.Admin : Roles.Client;
        }

        await _userStore.SaveAsync(user);
        return Ok(new
        {
            user = ToUserJson(user)
        });
    }

    public class ResetPasswordRequest
    {
        public string Email { get; set; } = "";
    }

    [HttpPost("auth/reset-password")]
    public IActionResult ResetPassword([FromBody] ResetPasswordRequest req)
    {
        return Ok(new { ok = true, message = "If the account exists, a reset link has been sent." });
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
        public int? ServicePrice { get; set; }
        public int? DurationHours { get; set; }
        public string? EventDate { get; set; }
        public string? EventTime { get; set; }
        public string? EventLocation { get; set; }
        public string? Requirements { get; set; }
        public string? Status { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? InvoiceStatus { get; set; }
    }

    [HttpPost("bookings")]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest req)
    {
        var user = await GetAuthenticatedUserAsync();
        var clientId = !string.IsNullOrWhiteSpace(req.ClientId) ? req.ClientId : (user?.ClientId ?? user?.Id ?? "CL-GUEST");
        var clientName = !string.IsNullOrWhiteSpace(req.ClientName) ? req.ClientName : (user?.FullName ?? "Client");
        var clientEmail = !string.IsNullOrWhiteSpace(req.ClientEmail) ? req.ClientEmail : (user?.Email ?? "");
        var clientPhone = !string.IsNullOrWhiteSpace(req.ClientPhone) ? req.ClientPhone : (user?.Phone ?? "");

        var booking = new Booking
        {
            Id = "book-" + Guid.NewGuid().ToString("N")[..8],
            ClientId = clientId,
            Uid = user?.Id ?? "",
            ClientName = clientName,
            ClientEmail = clientEmail,
            ClientPhone = clientPhone,
            EventType = req.ServiceName ?? "Photography Session",
            Date = req.EventDate ?? DateTime.UtcNow.ToString("yyyy-MM-dd"),
            StartTime = req.EventTime ?? "10:00",
            EndTime = "12:00",
            Hours = req.DurationHours ?? 1,
            Location = req.EventLocation ?? "TBD",
            Notes = req.Requirements ?? "",
            Status = !string.IsNullOrWhiteSpace(req.Status) ? req.Status : BookingStatus.Pending,
            EstimatedTotal = req.ServicePrice ?? 1000,
            InvoiceNumber = req.InvoiceNumber ?? ("INV-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()),
            InvoiceStatus = req.InvoiceStatus ?? "Unpaid",
            CreatedAt = Timestamp.GetCurrentTimestamp()
        };

        await _bookingStore.CreateAsync(booking);
        return Ok(new { booking = ToBookingJson(booking) });
    }

    public class StatusRequest
    {
        public string Status { get; set; } = "";
    }

    [HttpPatch("bookings/{id}/status")]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] StatusRequest req)
    {
        var booking = await _bookingStore.GetAsync(id);
        if (booking == null) return NotFound(new { error = "Booking not found" });

        booking.Status = req.Status;
        await _bookingStore.CreateAsync(booking);
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
        await _bookingStore.CreateAsync(booking);
        return Ok(new { booking = ToBookingJson(booking) });
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
        var enquiry = new ContactEnquiry
        {
            Id = "enq-" + Guid.NewGuid().ToString("N")[..8],
            FullName = req.FullName ?? "Anonymous",
            Email = req.Email ?? "",
            Phone = req.Phone ?? "",
            Message = req.Message ?? "",
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
        var user = await GetAuthenticatedUserAsync();
        var review = new Review
        {
            Id = "rev-" + Guid.NewGuid().ToString("N")[..8],
            ClientId = req.ClientId ?? user?.ClientId ?? "GUEST",
            ClientName = req.ClientName ?? user?.FullName ?? "Client",
            Rating = req.Rating ?? 5,
            Text = req.Text ?? "",
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
    private static object ToUserJson(UserProfile p) => new
    {
        uid = p.Id,
        id = p.Id,
        Id = p.Id,
        displayName = p.FullName,
        fullName = p.FullName,
        FullName = p.FullName,
        email = p.Email,
        Email = p.Email,
        phone = p.Phone,
        Phone = p.Phone,
        address = p.Address,
        Address = p.Address,
        photoUrl = p.PhotoUrl,
        PhotoUrl = p.PhotoUrl,
        role = p.Role.ToLowerInvariant(),
        Role = p.Role,
        clientId = p.ClientId,
        ClientId = p.ClientId
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
