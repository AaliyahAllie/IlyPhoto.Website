using System.ComponentModel.DataAnnotations;

namespace IlyPhotoWeb.Models;

// ---------- Account ----------
public class LoginViewModel
{
    [Required(ErrorMessage = "Please enter your email address."), EmailAddress]
    public string Email { get; set; } = "admin@ilyphoto.co.za";

    [Required(ErrorMessage = "Please enter your password.")]
    public string Password { get; set; } = "Password123!";

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Please enter your full name."), StringLength(100)]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Please enter your email address."), EmailAddress, StringLength(200)]
    public string Email { get; set; } = "";

    [Phone, StringLength(30)]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Please choose a password."), StringLength(100, MinimumLength = 6, ErrorMessage = "Your password must be at least 6 characters.")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Please confirm your password."), Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = "";
}

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Please enter your registered email address."), EmailAddress(ErrorMessage = "Please enter a valid email address."), StringLength(200)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Please choose a new password."), StringLength(100, MinimumLength = 6, ErrorMessage = "Your new password must be at least 6 characters.")]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "Please confirm your new password."), Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = "";
}

// ---------- Client ----------
public class BookingViewModel
{
    [Required] public string EventType { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.Today.AddDays(14);
    [Required] public string StartTime { get; set; } = "14:00";
    [Required] public string EndTime { get; set; } = "17:00";
    public int Hours { get; set; } = 3;   // recalculated on server
    [Required(ErrorMessage = "Please enter the event location."), StringLength(200)]
    public string Location { get; set; } = "Cape Town";
    [StringLength(1000)] public string Notes { get; set; } = "";
}

public class ClientEditBookingViewModel
{
    [Required] public string Id { get; set; } = "";
    [Required] public string EventType { get; set; } = "";
    [Required] public string Date { get; set; } = "";
    [Required] public string StartTime { get; set; } = "14:00";
    [Required] public string EndTime { get; set; } = "17:00";
    [Required(ErrorMessage = "Please enter the event location."), StringLength(200)]
    public string Location { get; set; } = "";
    [StringLength(1000)] public string Notes { get; set; } = "";
}

public class BookingPageViewModel
{
    public BookingViewModel Form { get; set; } = new();
    public List<ServicePackage> Services { get; set; } = new();
    public List<Booking> Mine { get; set; } = new();
}

public class ClientDashboardViewModel
{
    public Booking? Next { get; set; }
    public int UpcomingCount { get; set; }
    public int OutstandingCount { get; set; }
    public double OutstandingTotal { get; set; }
    public int GalleryCount { get; set; }
    public List<NotificationItem> Notifications { get; set; } = new();
}

public class InvoiceSheetModel
{
    public Invoice Invoice { get; set; } = new();
    public string ClientName { get; set; } = "";
    public string ClientEmail { get; set; } = "";
    public string ClientPhone { get; set; } = "";
    public string BankDetails { get; set; } = "";
}

public class GalleryCardViewModel
{
    public Gallery Gallery { get; set; } = new();
    public string? CoverUrl { get; set; }
}

public class AlbumViewModel
{
    public Gallery Gallery { get; set; } = new();
    public List<string> PhotoUrls { get; set; } = new();
}

// ---------- Admin ----------
public class AdminDashboardViewModel
{
    public int PendingCount { get; set; }
    public int UpcomingThisMonth { get; set; }
    public int OutstandingCount { get; set; }
    public double OutstandingTotal { get; set; }
    public int ClientCount { get; set; }
    public List<Booking> Pending { get; set; } = new();
}

public class AdminBookingsViewModel
{
    public List<Booking> Pending { get; set; } = new();
    public List<Booking> Upcoming { get; set; } = new();
}

public class AdminInvoicesViewModel
{
    public List<InvoiceSheetModel> Invoices { get; set; } = new();
    public List<UserProfile> Clients { get; set; } = new();
    public List<Booking> Bookings { get; set; } = new();
    public List<ServicePackage> Services { get; set; } = new();
}

public class AdminGalleriesViewModel
{
    public List<Gallery> Galleries { get; set; } = new();
    public List<UserProfile> Clients { get; set; } = new();
    public List<Booking> Bookings { get; set; } = new();
}

public class ClientRow
{
    public UserProfile Profile { get; set; } = new();
    public int BookingCount { get; set; }
    public string LastActivity { get; set; } = "";
}

public class InvoiceForm
{
    public string ClientId { get; set; } = "";
    public string? BookingId { get; set; }
    public string IssueDate { get; set; } = "";
    public string DueDate { get; set; } = "";
    public string Service { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public string Price { get; set; } = "";
    public string Status { get; set; } = InvoiceStatus.Unpaid;
}

public class ServiceForm
{
    public string? Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string BasePrice { get; set; } = "";
    public int IncludedHours { get; set; } = 1;
    public string ExtraHourPrice { get; set; } = "0";
    public bool HourlyOnly { get; set; }
    public int SortOrder { get; set; } = 10;
}

// ---------- Diagnostics ----------
public record DiagnosticCheck(string Name, string Status, string Detail);

// ---------- Contact ----------
public class ContactViewModel
{
    [Required(ErrorMessage = "Please enter your name."), StringLength(100)]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Please enter your email."), EmailAddress, StringLength(150)]
    public string Email { get; set; } = "";

    [Phone, StringLength(30)]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Please write a message."), StringLength(2000)]
    public string Message { get; set; } = "";
}
