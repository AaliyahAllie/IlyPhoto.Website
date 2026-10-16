using IlyPhotoWeb.Models;
using Microsoft.Extensions.Options;

namespace IlyPhotoWeb.Services;

public interface IEmailService
{
    Task<bool> SendInvoiceEmailAsync(Invoice inv, UserProfile client);
    Task<bool> SendBookingConfirmationEmailAsync(Booking booking, UserProfile client);
}

public class EmailService : IEmailService
{
    private readonly BusinessOptions _business;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<BusinessOptions> business, IWebHostEnvironment env, ILogger<EmailService> logger)
    {
        _business = business.Value;
        _env = env;
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
        .tagline {{ font-size: 13px; color: #7A6B5D; }}
        .invoice-title {{ font-size: 20px; font-weight: 700; color: #1A1A1A; margin-top: 15px; }}
        .details-table {{ width: 100%; border-collapse: collapse; margin: 20px 0; }}
        .details-table th, .details-table td {{ padding: 10px; border-bottom: 1px solid #ECE4D8; text-align: left; font-size: 14px; }}
        .details-table th {{ background: #FAF7F2; color: #4A3525; }}
        .total-row {{ font-weight: bold; font-size: 16px; background: #FAF7F2; }}
        .eft-box {{ background: #F8F5EE; border-left: 4px solid #4A3525; padding: 15px; border-radius: 4px; margin-top: 25px; }}
        .eft-title {{ font-weight: bold; color: #4A3525; margin-bottom: 8px; }}
        .footer {{ font-size: 12px; color: #888; text-align: center; margin-top: 30px; border-top: 1px solid #ECE4D8; padding-top: 15px; }}
    </style>
</head>
<body>
    <div class='card'>
        <div class='header'>
            <div class='logo'>ILY PHOTO</div>
            <div class='tagline'>Capture forever. • Cape Town, South Africa</div>
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
            <div class='eft-title'>🏦 Direct EFT Payment Instructions:</div>
            <pre style='font-family: inherit; font-size: 13px; margin: 0; line-height: 1.6; white-space: pre-wrap;'>{bankDetails}</pre>
            <p style='margin: 8px 0 0 0; font-size: 12px; color: #555;'>
                <strong>Important:</strong> Please use <strong>{inv.InvoiceNo}</strong> as the payment reference when initiating your bank transfer. Once payment is completed, kindly email your proof of payment (POP) to <a href='mailto:info@ilyphoto.co.za'>info@ilyphoto.co.za</a>.
            </p>
        </div>

        <div class='footer'>
            &copy; {DateTime.UtcNow.Year} ILY Photo Studio. All rights reserved.<br/>
            POPIA Compliant • Cape Town, South Africa
        </div>
    </div>
</body>
</html>";

            var fileName = $"{inv.InvoiceNo}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.html";
            var filePath = Path.Combine(emailDir, fileName);
            await File.WriteAllTextAsync(filePath, emailHtml);

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
        <h2 style='color: #4A3525;'>ILY PHOTO – Booking Update</h2>
        <p>Hi {client.FullName},</p>
        <p>Your photography booking for <strong>{booking.EventType}</strong> on <strong>{Format.Date(booking.Date)}</strong> ({booking.StartTime} - {booking.EndTime}) has been <strong>{booking.Status}</strong>.</p>
        <p>Location: {booking.Location}</p>
        <p>Thank you for partnering with ILY Photo!</p>
    </div>
</body>
</html>";

            var fileName = $"Booking_{booking.Id}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.html";
            await File.WriteAllTextAsync(Path.Combine(emailDir, fileName), emailHtml);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send booking notification email");
            return false;
        }
    }
}
