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

    private static bool IsConfirmedBooking(string? status) => BookingStatus.IsConfirmed(status);
    private static bool IsPendingBooking(string? status) => BookingStatus.IsPending(status);

    // ---------- Dashboard ----------
    public async Task<IActionResult> Index()
    {
        var today = Format.TodaySA();
        var todayIso = Format.Iso(today);
        var monthEnd = Format.Iso(new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1));

        var bookings = await _bookings.ListAllAsync();
        var invoices = await _invoices.ListAllAsync();
        var pending = bookings.Where(b => IsPendingBooking(b.Status)).OrderBy(b => b.Date).ThenBy(b => b.StartTime).ToList();
        var upcomingAll = bookings
            .Where(b => IsConfirmedBooking(b.Status) && string.CompareOrdinal(b.Date, todayIso) >= 0)
            .OrderBy(b => b.Date)
            .ThenBy(b => b.StartTime)
            .ToList();
        var outstanding = invoices.Where(i => !i.Status.Equals(InvoiceStatus.Paid, StringComparison.OrdinalIgnoreCase)).ToList();

        return View(new AdminDashboardViewModel
        {
            PendingCount = pending.Count,
            UpcomingThisMonth = upcomingAll.Count(b => string.CompareOrdinal(b.Date, monthEnd) <= 0),
            UpcomingTotalCount = upcomingAll.Count,
            OutstandingCount = outstanding.Count,
            OutstandingTotal = outstanding.Sum(i => i.Total),
            ClientCount = (await _users.ListClientsAsync()).Count,
            Pending = pending.Take(5).ToList(),
            Upcoming = upcomingAll.Take(5).ToList()
        });
    }

    // ---------- Bookings & Availability ----------
    public async Task<IActionResult> Bookings()
    {
        var all = await _bookings.ListAllAsync();
        return View(new AdminBookingsViewModel
        {
            Pending = all.Where(b => IsPendingBooking(b.Status)).OrderBy(b => b.Date).ThenBy(b => b.StartTime).ToList(),
            Upcoming = all.Where(b => IsConfirmedBooking(b.Status)).OrderBy(b => b.Date).ThenBy(b => b.StartTime).ToList()
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
        if (b is null || !IsPendingBooking(b.Status))
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
            b.Status = BookingStatus.Confirmed;
            await _bookings.SetStatusAsync(id, BookingStatus.Confirmed);

            var client = await _users.GetByClientIdAsync(b.ClientId)
                         ?? await _users.GetAsync(b.Uid)
                         ?? await _users.GetByEmailAsync(b.ClientEmail);

            if (client is not null)
            {
                if (string.IsNullOrEmpty(b.ClientId))
                {
                    b.ClientId = client.ClientId;
                    await _bookings.CreateAsync(b);
                }

                // Automatically generate invoice upon approval so client can settle and gallery can be linked
                var existingInvoices = await _invoices.ListAllAsync();
                var inv = existingInvoices.FirstOrDefault(i => i.BookingId == b.Id);
                if (inv == null)
                {
                    inv = await _invoices.CreateAsync(new Invoice
                    {
                        ClientId = client.ClientId,
                        BookingId = b.Id,
                        BookingLabel = b.Label,
                        IssueDate = Format.TodayIso(),
                        DueDate = b.Date,
                        Service = b.EventType,
                        Quantity = 1,
                        Price = b.EstimatedTotal,
                        Total = b.EstimatedTotal,
                        Status = b.EstimatedTotal == 0 ? InvoiceStatus.Paid : InvoiceStatus.Unpaid,
                        EmailSent = true
                    });
                    b.InvoiceNumber = inv.InvoiceNo;
                    b.InvoiceStatus = inv.Status;
                    await _bookings.CreateAsync(b);
                }

                await _emails.SendBookingConfirmationEmailAsync(b, client);
            }
            TempData["AdminMessage"] = $"Booking for {b.ClientName} ({b.EventType}) has been confirmed and invoice generated!";
        }
        return BackTo(returnTo);
    }

    [HttpPost]
    public async Task<IActionResult> DenyBooking(string id, string? returnTo)
    {
        var b = await _bookings.GetAsync(id);
        if (b is not null && IsPendingBooking(b.Status))
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

        if (b is null || (!IsPendingBooking(b.Status) && !IsConfirmedBooking(b.Status)))
            TempData["AdminError"] = "That booking can no longer be rescheduled.";
        else if (!Format.TryParseDate(date, out var d) || d < Format.TodaySA() || hours is null)
            TempData["AdminError"] = "Please select a valid future date and start/end times.";
        else if (await _bookings.IsBlockedAsync(Format.Iso(d)) || await _bookings.HasConfirmedOnDateAsync(Format.Iso(d), b.Id))
            TempData["AdminError"] = "That selected date is not available.";
        else
        {
            var normalizedIso = Format.Iso(d);
            var service = services.FirstOrDefault(s => s.Name.Equals(b.EventType, StringComparison.OrdinalIgnoreCase));
            var total = service?.PriceFor(hours.Value) ?? b.EstimatedTotal;
            await _bookings.RescheduleAsync(id, normalizedIso, startTime, endTime, hours.Value, total);
            TempData["AdminMessage"] = $"Booking moved to {Format.Date(normalizedIso)} ({startTime} - {endTime}).";
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
        var byId = clients.ToDictionary(c => c.ClientId, c => c, StringComparer.OrdinalIgnoreCase);
        var invoices = await _invoices.ListAllAsync();
        var bookings = (await _bookings.ListAllAsync()).Where(b => IsConfirmedBooking(b.Status)).ToList();

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

        bool bookingBelongsToClient = booking is null ||
            (client is not null && (
                booking.ClientId.Equals(client.ClientId, StringComparison.OrdinalIgnoreCase) ||
                booking.Uid.Equals(client.Id, StringComparison.OrdinalIgnoreCase) ||
                booking.ClientEmail.Equals(client.Email, StringComparison.OrdinalIgnoreCase)
            ));

        if (client is null)
            TempData["AdminError"] = "Please select a valid client.";
        else if (!bookingBelongsToClient)
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
                IssueDate = Format.NormalizeDateIso(form.IssueDate),
                DueDate = Format.NormalizeDateIso(form.DueDate),
                Service = form.Service.Trim(),
                Quantity = form.Quantity,
                Price = price,
                Status = form.Status,
                EmailSent = true
            });

            if (booking is not null)
            {
                await _bookings.SetInvoiceStatusAsync(booking.Id, inv.InvoiceNo, inv.Status);
            }

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
            var inv = await _invoices.GetAsync(id);
            if (inv is not null && !string.IsNullOrWhiteSpace(inv.BookingId))
            {
                await _bookings.SetInvoiceStatusAsync(inv.BookingId, inv.InvoiceNo, status);
            }
            TempData["AdminMessage"] = $"Invoice payment status updated to {status}.";
        }
        return RedirectToAction(nameof(Invoices));
    }

    // ---------- Galleries ----------
    public async Task<IActionResult> Galleries()
    {
        var allBookings = await _bookings.ListAllAsync();
        var confirmedBookings = allBookings
            .Where(b => IsConfirmedBooking(b.Status))
            .ToList();

        var clients = await _users.ListClientsAsync();
        foreach (var b in confirmedBookings)
        {
            if (!string.IsNullOrEmpty(b.ClientId) && !clients.Any(c => c.ClientId.Equals(b.ClientId, StringComparison.OrdinalIgnoreCase)))
            {
                var c = await _users.GetByClientIdAsync(b.ClientId) ?? await _users.GetAsync(b.Uid) ?? await _users.GetByEmailAsync(b.ClientEmail);
                if (c != null && !clients.Any(x => x.Id == c.Id)) clients.Add(c);
            }
        }

        return View(new AdminGalleriesViewModel
        {
            Galleries = await _galleries.ListAllAsync(),
            Clients = clients.OrderBy(c => c.FullName).ToList(),
            Bookings = confirmedBookings.OrderByDescending(b => b.Date).ToList()
        });
    }

    [HttpPost]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<IActionResult> UploadGallery(
        string? clientId,
        string? bookingId,
        string title,
        bool allowPortfolio,
        IFormFileCollection photos,
        CancellationToken ct)
    {
        Booking? booking = string.IsNullOrEmpty(bookingId) ? null : await _bookings.GetAsync(bookingId);
        UserProfile? client = null;

        if (!string.IsNullOrEmpty(clientId))
        {
            client = await _users.GetByClientIdAsync(clientId) ?? await _users.GetAsync(clientId);
        }

        if (client is null && booking is not null)
        {
            client = await _users.GetByClientIdAsync(booking.ClientId)
                     ?? await _users.GetAsync(booking.Uid)
                     ?? await _users.GetByEmailAsync(booking.ClientEmail);
        }

        if (client is null)
        {
            TempData["AdminError"] = "Please select a valid registered client.";
            return RedirectToAction(nameof(Galleries));
        }

        if (string.IsNullOrWhiteSpace(title) || title.Length > 150)
        {
            TempData["AdminError"] = "Please provide an album title.";
            return RedirectToAction(nameof(Galleries));
        }

        if (photos is null || photos.Count == 0)
        {
            TempData["AdminError"] = "Please select at least one photo using the file explorer button.";
            return RedirectToAction(nameof(Galleries));
        }

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
            var mine = bookings.Where(b =>
                (!string.IsNullOrWhiteSpace(c.ClientId) && b.ClientId.Equals(c.ClientId, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(c.Id) && b.Uid.Equals(c.Id, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(c.Email) && b.ClientEmail.Equals(c.Email, StringComparison.OrdinalIgnoreCase))
            ).ToList();
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
