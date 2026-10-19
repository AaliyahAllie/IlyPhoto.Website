using System.Globalization;
using System.Text.Json.Serialization;

namespace IlyPhotoWeb.Models;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Client = "Client";
    public const string Photographer = "Photographer";
}

public static class BookingStatus
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Denied = "Denied";
    public const string Cancelled = "Cancelled";
    public const string Completed = "Completed";
    public const string Rescheduled = "Rescheduled";
}

public static class InvoiceStatus
{
    public const string Unpaid = "Unpaid";
    public const string PartiallyPaid = "Partially Paid";
    public const string Paid = "Paid";
    public const string Overdue = "Overdue";
    public static readonly string[] All = { Unpaid, PartiallyPaid, Paid, Overdue };
}

/// <summary>
/// Cross-platform compatible UTC timestamp struct used for Azure Table Storage entity serialization.
/// </summary>
public readonly struct Timestamp : IEquatable<Timestamp>
{
    public DateTime UtcDateTime { get; }

    public Timestamp(DateTime dateTime)
    {
        UtcDateTime = dateTime.Kind == DateTimeKind.Utc ? dateTime : dateTime.ToUniversalTime();
    }

    public DateTime ToDateTime() => UtcDateTime;
    public DateTimeOffset ToDateTimeOffset() => new DateTimeOffset(UtcDateTime);
    public static Timestamp GetCurrentTimestamp() => new(DateTime.UtcNow);

    public override string ToString() => UtcDateTime.ToString("o", CultureInfo.InvariantCulture);

    public static implicit operator Timestamp(DateTime dt) => new(dt);
    public static implicit operator DateTime(Timestamp ts) => ts.UtcDateTime;

    public bool Equals(Timestamp other) => UtcDateTime.Equals(other.UtcDateTime);
    public override bool Equals(object? obj) => obj is Timestamp ts && Equals(ts);
    public override int GetHashCode() => UtcDateTime.GetHashCode();
    public static bool operator ==(Timestamp left, Timestamp right) => left.Equals(right);
    public static bool operator !=(Timestamp left, Timestamp right) => !left.Equals(right);
}

// Azure Table: users
public class UserProfile
{
    public string Id { get; set; } = "";
    public string ClientId { get; set; } = "";   // CL-1001 (admin = "ADMIN")
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Role { get; set; } = Roles.Client;
    public string Address { get; set; } = "";
    public string PhotoUrl { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public Timestamp CreatedAt { get; set; } = Timestamp.GetCurrentTimestamp();
}

// Azure Table: bookings
public class Booking
{
    public string Id { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string Uid { get; set; } = "";
    public string ClientName { get; set; } = "";
    public string ClientEmail { get; set; } = "";
    public string ClientPhone { get; set; } = "";
    public string EventType { get; set; } = "";
    public string Date { get; set; } = "";        // yyyy-MM-dd
    public string StartTime { get; set; } = "";   // HH:mm
    public string EndTime { get; set; } = "";     // HH:mm
    public int Hours { get; set; }
    public string Location { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Status { get; set; } = BookingStatus.Pending;
    public double EstimatedTotal { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public string InvoiceStatus { get; set; } = "Unpaid";
    public Timestamp CreatedAt { get; set; } = Timestamp.GetCurrentTimestamp();

    public DateTime EventDate =>
        Format.TryParseDate(Date, out var d) ? d : DateTime.Today;
    public string Label => $"{EventType} — {Format.Date(Date)}";

    public bool BelongsToClient(string? clientId, string? email = null, string? uid = null, string? fullName = null)
    {
        if (!string.IsNullOrWhiteSpace(clientId))
        {
            if (string.Equals(ClientId, clientId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Uid, clientId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ClientEmail, clientId, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        if (!string.IsNullOrWhiteSpace(email))
        {
            var cleanEmail = email.Trim();
            if (string.Equals(ClientEmail, cleanEmail, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ClientId, cleanEmail, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Uid, cleanEmail, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        if (!string.IsNullOrWhiteSpace(uid))
        {
            if (string.Equals(Uid, uid, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ClientId, uid, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        if (!string.IsNullOrWhiteSpace(fullName) && string.IsNullOrWhiteSpace(ClientEmail))
        {
            if (string.Equals(ClientName, fullName.Trim(), StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

// Azure Table: blockedDates
public class BlockedDate
{
    public string Id { get; set; } = "";
    public string Date { get; set; } = "";
    public string Reason { get; set; } = "";
    public Timestamp CreatedAt { get; set; } = Timestamp.GetCurrentTimestamp();
}

// Azure Table: invoices
public class Invoice
{
    public string Id { get; set; } = "";
    public string InvoiceNo { get; set; } = "";   // INV-00001
    public string ClientId { get; set; } = "";
    public string BookingId { get; set; } = "";
    public string BookingLabel { get; set; } = "";
    public string IssueDate { get; set; } = "";
    public string DueDate { get; set; } = "";
    public string Service { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public double Price { get; set; }
    public double Total { get; set; }
    public string Status { get; set; } = InvoiceStatus.Unpaid;
    public bool EmailSent { get; set; } = true;
    public Timestamp CreatedAt { get; set; } = Timestamp.GetCurrentTimestamp();
}

// Azure Table: galleries & Azure Blob: ilyphoto-galleries
public class Gallery
{
    public string Id { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string BookingId { get; set; } = "";
    public string Title { get; set; } = "";
    public string EventDate { get; set; } = "";
    public List<string> PhotoPaths { get; set; } = new();   // Storage object names
    public bool PortfolioApproved { get; set; }
    public Timestamp CreatedAt { get; set; } = Timestamp.GetCurrentTimestamp();

    public int PhotoCount => PhotoPaths.Count;
    public string? CoverPath => PhotoPaths.FirstOrDefault();
}

// Azure Table: services
public class ServicePackage
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public double BasePrice { get; set; }
    public int IncludedHours { get; set; }
    public double ExtraHourPrice { get; set; }
    public bool HourlyOnly { get; set; }
    public int SortOrder { get; set; }

    public double PriceFor(int hours)
    {
        hours = Math.Max(1, hours);
        if (HourlyOnly)
        {
            return hours * BasePrice;
        }
        return BasePrice + Math.Max(0, hours - IncludedHours) * ExtraHourPrice;
    }
}

// Azure Table: reviews
public class Review
{
    public string Id { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientName { get; set; } = "";
    public int Rating { get; set; }
    public string Text { get; set; } = "";
    public bool Approved { get; set; }   // for public display
    public Timestamp CreatedAt { get; set; } = Timestamp.GetCurrentTimestamp();
}

// Azure Table: enquiries
public class ContactEnquiry
{
    public string Id { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Message { get; set; } = "";
    public Timestamp CreatedAt { get; set; } = Timestamp.GetCurrentTimestamp();
}

// Azure Table: notifications
public class NotificationItem
{
    public string Id { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string RecipientEmail { get; set; } = "";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string Category { get; set; } = "Booking"; // Booking, Invoice, Gallery, Security
    public string RelatedId { get; set; } = "";
    public bool IsRead { get; set; }
    public Timestamp CreatedAt { get; set; } = Timestamp.GetCurrentTimestamp();
}
