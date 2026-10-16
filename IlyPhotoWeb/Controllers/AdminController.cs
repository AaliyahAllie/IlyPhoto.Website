using IlyPhotoWeb.Models;
using IlyPhotoWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IlyPhotoWeb.Controllers;

[Authorize(Roles = Roles.Admin)]
public class AdminController : Controller
{
    private const long MaxUploadBytes = 2L * 1024 * 1024 * 1024;   // 2 GB per album upload

    private readonly BookingStore _bookings;
    private readonly InvoiceStore _invoices;
    private readonly GalleryStore _galleries;
    private readonly ServiceStore _services;
    private readonly UserStore _users;
    private readonly ReviewStore _reviews;
    private readonly IEmailService _emails;

    public AdminController(
        BookingStore bookings,
        InvoiceStore invoices,
        GalleryStore galleries,
        ServiceStore services,
        UserStore users,
        ReviewStore reviews,
        IEmailService emails)
    {
        _bookings = bookings;
        _invoices = invoices;
        _galleries = galleries;
        _services = services;
        _users = users;
        _reviews = reviews;
        _emails = emails;
    }

    // ---------- Dashboard ----------
    public async Task<IActionResult> Index()
    {
        var today = Format.TodaySA();
        var todayIso = Format.Iso(today);
        var monthEnd = Format.Iso(new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1));

        var bookings = await _bookings.ListAllAsync();
        var invoices = await _invoices.ListAllAsync();
        var pending = bookings.Where(b => b.Status == BookingStatus.Pending).ToList();
        var outstanding = invoices.Where(i => i.Status != InvoiceStatus.Paid).ToList();

        return View(new AdminDashboardViewModel
        {
            PendingCount = pending.Count,
            UpcomingThisMonth = bookings.Count(b => b.Status == BookingStatus.Confirmed
                && string.CompareOrdinal(b.Date, todayIso) >= 0 && string.CompareOrdinal(b.Date, monthEnd) <= 0),
            OutstandingCount = outstanding.Count,
            OutstandingTotal = outstanding.Sum(i => i.Total),
            ClientCount = (await _users.ListClientsAsync()).Count,
            Pending = pending.Take(5).ToList()
        });
    }

    // ---------- Bookings & Availability ----------
    public async Task<IActionResult> Bookings()
    {
        var todayIso = Format.TodayIso();
        var all = await _bookings.ListAllAsync();
        return View(new AdminBookingsViewModel
        {
            Pending = all.Where(b => b.Status == BookingStatus.Pending).ToList(),
            Upcoming = all.Where(b => b.Status == BookingStatus.Confirmed && string.CompareOrdinal(b.Date, todayIso) >= 0).ToList()
        });
    }

    public async Task<IActionResult> Calendar()
    {
        var bookings = await _bookings.ListAllAsync();
        return View(bookings);
    }

    [HttpPost]
    public async Task<IActionResult> ApproveBooking(string id, string? returnTo)
    {
        var b = await _bookings.GetAsync(id);
        if (b is null || b.Status != BookingStatus.Pending)
        {
            TempData["AdminError"] = "That request is no longer pending.";
        }
        else if (await _bookings.IsBlockedAsync(b.Date))
        {
            TempData["AdminError"] = "That date is blocked. Please unblock it or reschedule the booking.";
        }
        else if (await _bookings.HasConfirmedOnDateAsync(b.Date, b.Id))
        {
            TempData["AdminError"] = "A confirmed booking already exists on that date. Please reschedule or deny this request.";
        }
        else
        {
            await _bookings.SetStatusAsync(id, BookingStatus.Confirmed);
            var client = await _users.GetByClientIdAsync(b.ClientId);
            if (client is not null)
            {
                await _emails.SendBookingConfirmationEmailAsync(b, client);
            }
            TempData["AdminMessage"] = $"Booking for {b.ClientName} ({b.EventType}) has been confirmed!";
        }
        return BackTo(returnTo);
    }

    [HttpPost]
    public async Task<IActionResult> DenyBooking(string id, string? returnTo)
    {
        var b = await _bookings.GetAsync(id);
        if (b is not null && b.Status == BookingStatus.Pending)
        {
            await _bookings.SetStatusAsync(id, BookingStatus.Denied);
            TempData["AdminMessage"] = $"Booking for {b.ClientName} was denied.";
        }
        return BackTo(returnTo);
    }

    [HttpPost]
    public async Task<IActionResult> RescheduleBooking(string id, string date, string startTime, string endTime, string? returnTo)
    {
        var b = await _bookings.GetAsync(id);
        var services = await _services.ListAsync();
        var hours = Format.HoursBetween(startTime, endTime);

        if (b is null || (b.Status != BookingStatus.Pending && b.Status != BookingStatus.Confirmed))
            TempData["AdminError"] = "That booking can no longer be rescheduled.";
        else if (!Format.TryParseDate(date, out var d) || d < Format.TodaySA() || hours is null)
            TempData["AdminError"] = "Please select a valid future date and start/end times.";
        else if (await _bookings.IsBlockedAsync(date) || await _bookings.HasConfirmedOnDateAsync(date, b.Id))
            TempData["AdminError"] = "That selected date is not available.";
        else
        {
            var service = services.FirstOrDefault(s => s.Name == b.EventType);
            var total = service?.PriceFor(hours.Value) ?? b.EstimatedTotal;
            await _bookings.RescheduleAsync(id, date, startTime, endTime, hours.Value, total);
            TempData["AdminMessage"] = $"Booking moved to {Format.Date(date)} ({startTime} - {endTime}).";
        }
        return BackTo(returnTo);
    }

    [HttpPost]
    public async Task<IActionResult> BlockDate(string date, string? reason)
    {
        if (!Format.TryParseDate(date, out _))
            TempData["AdminError"] = "Please select a valid date.";
        else if (await _bookings.HasConfirmedOnDateAsync(date))
            TempData["AdminError"] = "You already have a confirmed booking on that date, so it cannot be blocked.";
        else
        {
            await _bookings.BlockAsync(date, (reason ?? "").Trim());
            TempData["AdminMessage"] = $"{Format.Date(date)} is now blocked on your calendar.";
        }
        return RedirectToAction(nameof(Bookings));
    }

    [HttpPost]
    public async Task<IActionResult> UnblockDate(string date)
    {
        if (Format.TryParseDate(date, out _))
        {
            await _bookings.UnblockAsync(date);
            TempData["AdminMessage"] = $"{Format.Date(date)} has been unblocked and is available again.";
        }
        return RedirectToAction(nameof(Bookings));
    }

    private IActionResult BackTo(string? returnTo) =>
        RedirectToAction(returnTo == "Index" ? nameof(Index) : nameof(Bookings));

    // ---------- Invoices ----------
    public async Task<IActionResult> Invoices()
    {
        var clients = await _users.ListClientsAsync();
        var byId = clients.ToDictionary(c => c.ClientId, c => c);
        var invoices = await _invoices.ListAllAsync();
        var bookings = (await _bookings.ListAllAsync()).Where(b => b.Status == BookingStatus.Confirmed).ToList();

        return View(new AdminInvoicesViewModel
        {
            Clients = clients,
            Bookings = bookings,
            Services = await _services.ListAsync(),
            Invoices = invoices.Select(i =>
            {
                byId.TryGetValue(i.ClientId, out var c);
                return new InvoiceSheetModel
                {
                    Invoice = i,
                    ClientName = c?.FullName ?? i.ClientId,
                    ClientEmail = c?.Email ?? "",
                    ClientPhone = c?.Phone ?? ""
                };
            }).ToList()
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateInvoice(InvoiceForm form)
    {
        var client = await _users.GetByClientIdAsync(form.ClientId ?? "");
        Booking? booking = null;
        if (!string.IsNullOrEmpty(form.BookingId)) booking = await _bookings.GetAsync(form.BookingId);

        if (client is null)
            TempData["AdminError"] = "Please select a valid client.";
        else if (booking is not null && booking.ClientId != client.ClientId)
            TempData["AdminError"] = "That booking does not belong to the selected client.";
        else if (!Format.TryParseDate(form.IssueDate, out _) || !Format.TryParseDate(form.DueDate, out _))
            TempData["AdminError"] = "Please enter valid issue and due dates.";
        else if (string.IsNullOrWhiteSpace(form.Service) || form.Service.Length > 200)
            TempData["AdminError"] = "Please enter the service or package description.";
        else if (form.Quantity < 1 || form.Quantity > 1000 || !Format.TryParseMoney(form.Price, out var price))
            TempData["AdminError"] = "Please enter a valid quantity and amount.";
        else if (!InvoiceStatus.All.Contains(form.Status))
            TempData["AdminError"] = "Please select a valid payment status.";
        else
        {
            Format.TryParseMoney(form.Price, out price);
            var inv = await _invoices.CreateAsync(new Invoice
            {
                ClientId = client.ClientId,
                BookingId = booking?.Id ?? "",
                BookingLabel = booking?.Label ?? "",
                IssueDate = form.IssueDate,
                DueDate = form.DueDate,
                Service = form.Service.Trim(),
                Quantity = form.Quantity,
                Price = price,
                Status = form.Status,
                EmailSent = true
            });

            // Immediately dispatch invoice via email to the client for EFT payment
            await _emails.SendInvoiceEmailAsync(inv, client);

            TempData["AdminMessage"] = $"Invoice {inv.InvoiceNo} generated and emailed to {client.FullName} ({client.Email}).";
        }
        return RedirectToAction(nameof(Invoices));
    }

    [HttpPost]
    public async Task<IActionResult> SetInvoiceStatus(string id, string status)
    {
        if (InvoiceStatus.All.Contains(status))
        {
            await _invoices.SetStatusAsync(id, status);
            TempData["AdminMessage"] = $"Invoice payment status updated to {status}.";
        }
        return RedirectToAction(nameof(Invoices));
    }

    // ---------- Galleries ----------
    public async Task<IActionResult> Galleries()
    {
        return View(new AdminGalleriesViewModel
        {
            Galleries = await _galleries.ListAllAsync(),
            Clients = await _users.ListClientsAsync(),
            Bookings = (await _bookings.ListAllAsync()).Where(b => b.Status == BookingStatus.Confirmed).ToList()
        });
    }

    [HttpPost]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<IActionResult> UploadGallery(
        string clientId,
        string? bookingId,
        string title,
        bool allowPortfolio,
        IFormFileCollection photos,
        CancellationToken ct)
    {
        var client = await _users.GetByClientIdAsync(clientId ?? "");
        Booking? booking = string.IsNullOrEmpty(bookingId) ? null : await _bookings.GetAsync(bookingId);

        if (client is null)
            TempData["AdminError"] = "Client not found.";
        else if (booking is not null && booking.ClientId != client.ClientId)
            TempData["AdminError"] = "That booking does not belong to the selected client.";
        else if (string.IsNullOrWhiteSpace(title) || title.Length > 150)
            TempData["AdminError"] = "Please provide an album title.";
        else if (photos is null || photos.Count == 0)
            TempData["AdminError"] = "Please select at least one photo using the file explorer button.";
        else
        {
            var (gallery, skipped) = await _galleries.CreateWithPhotosAsync(new Gallery
            {
                ClientId = client.ClientId,
                BookingId = booking?.Id ?? "",
                Title = title.Trim(),
                EventDate = booking?.Date ?? Format.TodayIso(),
                PortfolioApproved = allowPortfolio
            }, photos, ct);

            TempData["AdminMessage"] = $"Successfully uploaded {gallery.PhotoCount} photo(s) to \"{gallery.Title}\" for {client.FullName}."
                + (skipped > 0 ? $" ({skipped} files were skipped: only JPG/PNG/WEBP/GIF up to 50MB supported)." : "");
        }
        return RedirectToAction(nameof(Galleries));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteGallery(string id)
    {
        var g = await _galleries.GetAsync(id);
        if (g is not null)
        {
            await _galleries.DeleteAsync(g);
            TempData["AdminMessage"] = $"Album \"{g.Title}\" removed.";
        }
        return RedirectToAction(nameof(Galleries));
    }

    // ---------- Clients ----------
    public async Task<IActionResult> Clients()
    {
        var clients = await _users.ListClientsAsync();
        var bookings = await _bookings.ListAllAsync();
        var rows = clients.Select(c =>
        {
            var mine = bookings.Where(b => b.ClientId == c.ClientId).ToList();
            var last = mine.Select(b => b.CreatedAt.ToDateTime()).DefaultIfEmpty(c.CreatedAt.ToDateTime()).Max();
            return new ClientRow
            {
                Profile = c,
                BookingCount = mine.Count,
                LastActivity = last.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture)
            };
        }).ToList();
        return View(rows);
    }

    // ---------- Services & Pricing ----------
    public async Task<IActionResult> Services() => View(await _services.ListAsync());

    [HttpPost]
    public async Task<IActionResult> SaveService(ServiceForm form)
    {
        if (string.IsNullOrWhiteSpace(form.Name) || form.Name.Length > 80
            || !Format.TryParseMoney(form.BasePrice, out var basePrice)
            || !Format.TryParseMoney(form.ExtraHourPrice, out var extra)
            || form.IncludedHours < 1 || form.IncludedHours > 24)
        {
            TempData["AdminError"] = "Please enter valid package details and rates.";
            return RedirectToAction(nameof(Services));
        }

        await _services.SaveAsync(new ServicePackage
        {
            Id = form.Id ?? "",
            Name = form.Name.Trim(),
            Description = (form.Description ?? "").Trim(),
            BasePrice = basePrice,
            ExtraHourPrice = extra,
            IncludedHours = form.IncludedHours,
            HourlyOnly = form.HourlyOnly,
            SortOrder = form.SortOrder
        });
        TempData["AdminMessage"] = $"Package \"{form.Name.Trim()}\" updated successfully.";
        return RedirectToAction(nameof(Services));
    }

    // ---------- Reviews Admin ----------
    public async Task<IActionResult> Reviews()
    {
        var reviews = await _reviews.ListApprovedAsync();
        return View(reviews);
    }
}
