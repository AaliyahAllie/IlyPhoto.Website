using IlyPhotoWeb.Models;
using Microsoft.Extensions.Options;

namespace IlyPhotoWeb.Services;

public interface IEmailService
{
    Task<bool> SendInvoiceEmailAsync(Invoice inv, UserProfile client);
    Task<bool> SendBookingConfirmationEmailAsync(Booking booking, UserProfile client);
    Task<bool> SendBookingRescheduledEmailAsync(Booking booking, UserProfile client);
    Task<bool> SendGalleryReadyEmailAsync(Gallery gallery, UserProfile client);
    Task<bool> SendPasswordResetEmailAsync(UserProfile user, string? note = null);
}

public class EmailService : IEmailService
{
    private readonly BusinessOptions _business;
    private readonly IWebHostEnvironment _env;
    private readonly NotificationStore _notifications;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IOptions<BusinessOptions> business,
        IWebHostEnvironment env,
        NotificationStore notifications,
        ILogger<EmailService> logger)
    {
        _business = business.Value;
        _env = env;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<bool> SendInvoiceEmailAsync(Invoice inv, UserProfile client)
    {
        try
        {
            var emailDir = Path.Combine(_env.ContentRootPath, "App_Data", "sent_emails");
            Directory.CreateDirectory(emailDir);

            var bankDetails = string.IsNullOrWhiteSpace(_business.BankDetails)
                ? "Bank: Standard Bank\nAccount Holder: Ily Photo Studio (Aysha Allie)\nAccount Number: 10182736450\nBranch Code: 051001\nReference: " + inv.InvoiceNo
                : _business.BankDetails.Replace("your invoice number", inv.InvoiceNo);

            var emailHtml = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <style>
        body {{ font-family: 'DM Sans', Arial, sans-serif; background: #FAF7F2; color: #2C2C2C; padding: 20px; }}
        .card {{ max-width: 600px; margin: 0 auto; background: #FFFFFF; border: 1px solid #E2DACB; border-radius: 8px; padding: 30px; }}
        .header {{ border-bottom: 2px solid #4A3525; padding-bottom: 15px; margin-bottom: 20px; }}
        .logo {{ font-size: 24px; font-weight: bold; color: #4A3525; letter-spacing: 1px; }}
        .tagline {{ font-size: 13px; color: #5C4B3B; }}
        .invoice-title {{ font-size: 20px; font-weight: 700; color: #1A1A1A; margin-top: 15px; }}
        .details-table {{ width: 100%; border-collapse: collapse; margin: 20px 0; }}
        .details-table th, .details-table td {{ padding: 10px; border-bottom: 1px solid #ECE4D8; text-align: left; font-size: 14px; }}
        .details-table th {{ background: #FAF7F2; color: #4A3525; }}
        .total-row {{ font-weight: bold; font-size: 16px; background: #FAF7F2; }}
        .eft-box {{ background: #F8F5EE; border-left: 4px solid #4A3525; padding: 15px; border-radius: 4px; margin-top: 25px; }}
        .eft-title {{ font-weight: bold; color: #4A3525; margin-bottom: 8px; }}
        .footer {{ font-size: 12px; color: #555; text-align: center; margin-top: 30px; border-top: 1px solid #ECE4D8; padding-top: 15px; }}
    </style>
</head>
<body>
    <div class='card'>
        <div class='header'>
            <div class='logo'>ILY PHOTO</div>
            <div class='tagline'>Capture forever. • Strandfontein, Cape Town</div>
        </div>

        <div class='invoice-title'>Tax Invoice: {inv.InvoiceNo}</div>
        <p>Dear <strong>{client.FullName}</strong>,</p>
        <p>Thank you for choosing ILY Photo. Your photography invoice is ready. Please find the session details and EFT banking instructions below:</p>

        <table class='details-table'>
            <tr>
                <th>Invoice Number</th>
                <td>{inv.InvoiceNo}</td>
            </tr>
            <tr>
                <th>Client ID</th>
                <td>{client.ClientId}</td>
            </tr>
            <tr>
                <th>Issue Date</th>
                <td>{Format.Date(inv.IssueDate)}</td>
            </tr>
            <tr>
                <th>Due Date</th>
                <td>{Format.Date(inv.DueDate)}</td>
            </tr>
            <tr>
                <th>Service / Package</th>
                <td>{inv.Service}</td>
            </tr>
            <tr>
                <th>Quantity</th>
                <td>{inv.Quantity}</td>
            </tr>
            <tr>
                <th>Rate</th>
                <td>{Format.Rand(inv.Price)}</td>
            </tr>
            <tr class='total-row'>
                <td>Total Due (ZAR)</td>
                <td>{Format.Rand(inv.Total)}</td>
            </tr>
        </table>

        <div class='eft-box'>
            <div class='eft-title'>Direct EFT Payment Instructions:</div>
            <pre style='font-family: inherit; font-size: 13px; margin: 0; line-height: 1.6; white-space: pre-wrap;'>{bankDetails}</pre>
            <p style='margin: 8px 0 0 0; font-size: 12px; color: #444;'>
                <strong>Important:</strong> Please use <strong>{inv.InvoiceNo}</strong> as the payment reference when initiating your bank transfer. Once payment is completed, kindly email your proof of payment (POP) to <a href='mailto:info@ilyphoto.co.za'>info@ilyphoto.co.za</a>.
            </p>
        </div>

        <div class='footer'>
            &copy; {DateTime.UtcNow.Year} ILY Photo Studio. All rights reserved.<br/>
            POPIA Compliant • Strandfontein, Cape Town, South Africa
        </div>
    </div>
</body>
</html>";

            var fileName = $"{inv.InvoiceNo}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.html";
            var filePath = Path.Combine(emailDir, fileName);
            await File.WriteAllTextAsync(filePath, emailHtml);

            await _notifications.CreateAsync(new NotificationItem
            {
                ClientId = client.ClientId,
                RecipientEmail = client.Email,
                Title = $"Invoice Issued: {inv.InvoiceNo}",
                Message = $"Invoice {inv.InvoiceNo} for {inv.Service} ({Format.Rand(inv.Total)}) has been issued and is {inv.Status}. Due by {Format.Date(inv.DueDate)}.",
                Category = "Invoice",
                RelatedId = inv.Id
            });

            _logger.LogInformation("Invoice email dispatched to {Email} (archived to {Path})", client.Email, filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send invoice email for {InvoiceNo}", inv.InvoiceNo);
            return false;
        }
    }

    public async Task<bool> SendBookingConfirmationEmailAsync(Booking booking, UserProfile client)
    {
        try
        {
            var emailDir = Path.Combine(_env.ContentRootPath, "App_Data", "sent_emails");
            Directory.CreateDirectory(emailDir);

            var emailHtml = $@"<!DOCTYPE html>
<html>
<body style='font-family: sans-serif; background: #FAF7F2; padding: 20px;'>
    <div style='max-width: 600px; margin: 0 auto; background: #FFF; padding: 25px; border-radius: 8px; border: 1px solid #E2DACB;'>
        <h2 style='color: #4A3525;'>ILY PHOTO – Booking {booking.Status}</h2>
        <p>Hi {client.FullName},</p>
        <p>Your photography booking for <strong>{booking.EventType}</strong> on <strong>{Format.Date(booking.Date)}</strong> ({booking.StartTime} - {booking.EndTime}) is now <strong>{booking.Status}</strong>.</p>
        <p><strong>Venue / Location:</strong> {booking.Location}</p>
        <p><strong>Estimated Total:</strong> {Format.Rand(booking.EstimatedTotal)}</p>
        <p>Thank you for partnering with ILY Photo!</p>
    </div>
</body>
</html>";

            var fileName = $"Booking_{booking.Id}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.html";
            await File.WriteAllTextAsync(Path.Combine(emailDir, fileName), emailHtml);

            await _notifications.CreateAsync(new NotificationItem
            {
                ClientId = string.IsNullOrWhiteSpace(client.ClientId) ? booking.ClientId : client.ClientId,
                RecipientEmail = string.IsNullOrWhiteSpace(client.Email) ? booking.ClientEmail : client.Email,
                Title = $"Booking {booking.Status}: {booking.EventType}",
                Message = $"Your {booking.EventType} session on {Format.Date(booking.Date)} ({booking.StartTime}–{booking.EndTime}) at {booking.Location} is now {booking.Status}.",
                Category = "Booking",
                RelatedId = booking.Id
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send booking notification email");
            return false;
        }
    }

    public async Task<bool> SendBookingRescheduledEmailAsync(Booking booking, UserProfile client)
    {
        try
        {
            var emailDir = Path.Combine(_env.ContentRootPath, "App_Data", "sent_emails");
            Directory.CreateDirectory(emailDir);

            var emailHtml = $@"<!DOCTYPE html>
<html>
<body style='font-family: sans-serif; background: #FAF7F2; padding: 20px;'>
    <div style='max-width: 600px; margin: 0 auto; background: #FFF; padding: 25px; border-radius: 8px; border: 1px solid #E2DACB;'>
        <h2 style='color: #4A3525;'>ILY PHOTO – Booking Rescheduled & Updated</h2>
        <p>Hi {client.FullName},</p>
        <p>Your booking for <strong>{booking.EventType}</strong> has been updated with the following schedule and venue details:</p>
        <ul>
            <li><strong>New Date:</strong> {Format.Date(booking.Date)}</li>
            <li><strong>Time:</strong> {booking.StartTime} – {booking.EndTime} ({booking.Hours} hrs)</li>
            <li><strong>Location / Venue:</strong> {booking.Location}</li>
            <li><strong>Estimated Total:</strong> {Format.Rand(booking.EstimatedTotal)}</li>
            <li><strong>Status:</strong> {booking.Status}</li>
        </ul>
        <p>Thank you for choosing ILY Photo Studio.</p>
    </div>
</body>
</html>";

            var fileName = $"Reschedule_{booking.Id}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.html";
            await File.WriteAllTextAsync(Path.Combine(emailDir, fileName), emailHtml);

            await _notifications.CreateAsync(new NotificationItem
            {
                ClientId = string.IsNullOrWhiteSpace(client.ClientId) ? booking.ClientId : client.ClientId,
                RecipientEmail = string.IsNullOrWhiteSpace(client.Email) ? booking.ClientEmail : client.Email,
                Title = $"Booking Rescheduled: {booking.EventType}",
                Message = $"Updated to {Format.Date(booking.Date)} ({booking.StartTime}–{booking.EndTime}) at {booking.Location}. Total: {Format.Rand(booking.EstimatedTotal)}.",
                Category = "Booking",
                RelatedId = booking.Id
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send booking rescheduled email");
            return false;
        }
    }

    public async Task<bool> SendGalleryReadyEmailAsync(Gallery gallery, UserProfile client)
    {
        try
        {
            var emailDir = Path.Combine(_env.ContentRootPath, "App_Data", "sent_emails");
            Directory.CreateDirectory(emailDir);

            var emailHtml = $@"<!DOCTYPE html>
<html>
<body style='font-family: sans-serif; background: #FAF7F2; padding: 20px;'>
    <div style='max-width: 600px; margin: 0 auto; background: #FFF; padding: 25px; border-radius: 8px; border: 1px solid #E2DACB;'>
        <h2 style='color: #4A3525;'>ILY PHOTO – Your Gallery is Ready!</h2>
        <p>Hi {client.FullName},</p>
        <p>Your private photo album <strong>{gallery.Title}</strong> ({gallery.PhotoCount} high-resolution photos) has been uploaded and is ready to view and download in your Client Portal.</p>
    </div>
</body>
</html>";

            var fileName = $"Gallery_{gallery.Id}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.html";
            await File.WriteAllTextAsync(Path.Combine(emailDir, fileName), emailHtml);

            await _notifications.CreateAsync(new NotificationItem
            {
                ClientId = client.ClientId,
                RecipientEmail = client.Email,
                Title = $"Gallery Ready: {gallery.Title}",
                Message = $"{gallery.PhotoCount} edited photos for '{gallery.Title}' are now available for secure viewing and ZIP download in your Galleries portal.",
                Category = "Gallery",
                RelatedId = gallery.Id
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send gallery ready notification");
            return false;
        }
    }

    public async Task<bool> SendPasswordResetEmailAsync(UserProfile user, string? note = null)
    {
        try
        {
            var emailDir = Path.Combine(_env.ContentRootPath, "App_Data", "sent_emails");
            Directory.CreateDirectory(emailDir);

            var detail = string.IsNullOrWhiteSpace(note)
                ? "A password recovery request was processed for your ILY Photo account."
                : note;

            var emailHtml = $@"<!DOCTYPE html>
<html>
<body style='font-family: sans-serif; background: #FAF7F2; padding: 20px;'>
    <div style='max-width: 600px; margin: 0 auto; background: #FFF; padding: 25px; border-radius: 8px; border: 1px solid #E2DACB;'>
        <h2 style='color: #4A3525;'>ILY PHOTO – Password Recovery Notification</h2>
        <p>Hi {user.FullName},</p>
        <p>{detail}</p>
        <p>Account Email: <strong>{user.Email}</strong> (Client ID: {user.ClientId})</p>
    </div>
</body>
</html>";

            var fileName = $"PasswordReset_{user.Id}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.html";
            await File.WriteAllTextAsync(Path.Combine(emailDir, fileName), emailHtml);

            await _notifications.CreateAsync(new NotificationItem
            {
                ClientId = user.ClientId,
                RecipientEmail = user.Email,
                Title = "Password Recovery Processed",
                Message = detail,
                Category = "Security",
                RelatedId = user.Id
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email");
            return false;
        }
    }
}
