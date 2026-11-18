using System.Security.Claims;
using IlyPhotoWeb.Models;
using IlyPhotoWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace IlyPhotoWeb.Controllers;

[Authorize(Roles = Roles.Client)]
public class ClientController : Controller
{
    private readonly BookingStore _bookings;
    private readonly InvoiceStore _invoices;
    private readonly GalleryStore _galleries;
    private readonly ServiceStore _services;
    private readonly ReviewStore _reviews;
    private readonly UserStore _users;

    public ClientController(
        BookingStore bookings,
        InvoiceStore invoices,
        GalleryStore galleries,
        ServiceStore services,
        ReviewStore reviews,
        UserStore users)
    {
        _bookings = bookings;
        _invoices = invoices;
        _galleries = galleries;
        _services = services;
        _reviews = reviews;
        _users = users;
    }

    private string ClientIdValue => User.ClientId();
    private string ClientEmailValue => User.FindFirstValue(ClaimTypes.Email) ?? "";

    // ---------- Dashboard ----------
    public async Task<IActionResult> Index()
    {
        var today = Format.TodayIso();
        var bookings = await _bookings.ListByClientAsync(ClientIdValue, User.Uid(), ClientEmailValue);
        var invoices = await _invoices.ListByClientAsync(ClientIdValue);
        var galleries = await _galleries.ListByClientAsync(ClientIdValue);

        var upcoming = bookings
            .Where(b => (BookingStatus.IsPending(b.Status) || BookingStatus.IsConfirmed(b.Status))
                        && string.CompareOrdinal(b.Date, today) >= 0)
            .OrderBy(b => b.Date, StringComparer.Ordinal)
            .ThenBy(b => b.StartTime, StringComparer.Ordinal)
            .ToList();
        var outstanding = invoices.Where(i => i.Status != InvoiceStatus.Paid).ToList();

        var paidGalleryCount = 0;
        foreach (var g in galleries)
        {
            if (g.PhotoCount > 0)
            {
                var (isPaid, _, _) = await CheckGalleryPaymentAsync(g, invoices);
                if (isPaid) paidGalleryCount++;
            }
        }

        return View(new ClientDashboardViewModel
        {
            Next = upcoming.FirstOrDefault(),
            UpcomingCount = upcoming.Count,
            OutstandingCount = outstanding.Count,
            OutstandingTotal = outstanding.Sum(i => i.Total),
            GalleryCount = paidGalleryCount
        });
    }

    // ---------- Bookings ----------
    [HttpGet]
    public async Task<IActionResult> Bookings()
    {
        var services = await _services.ListAsync();
        return View(new BookingPageViewModel
        {
            Services = services,
            Form = new BookingViewModel { EventType = services.FirstOrDefault()?.Name ?? "", Date = Format.TodaySA().AddDays(14) },
            Mine = await _bookings.ListByClientAsync(ClientIdValue, User.Uid(), ClientEmailValue)
        });
    }

    [HttpPost]
    public async Task<IActionResult> Bookings(BookingViewModel form)
    {
        var services = await _services.ListAsync();
        var service = services.FirstOrDefault(s => s.Name.Equals(form.EventType?.Trim(), StringComparison.OrdinalIgnoreCase));
        var dateIso = Format.Iso(form.Date);
        int? hours = Format.HoursBetween(form.StartTime, form.EndTime);

        if (service is null) ModelState.AddModelError(nameof(form.EventType), "Please choose an event type.");
        if (hours is null) ModelState.AddModelError(nameof(form.EndTime), "The end time must be after the start time.");
        if (form.Date.Date <= Format.TodaySA()) ModelState.AddModelError(nameof(form.Date), "Please choose a future date.");

        if (ModelState.IsValid)
        {
            if (await _bookings.IsBlockedAsync(dateIso) || await _bookings.HasConfirmedOnDateAsync(dateIso))
                ModelState.AddModelError(nameof(form.Date), "Sorry, that date is not available. Please choose another date.");
        }

        if (!ModelState.IsValid)
        {
            return View(new BookingPageViewModel
            {
                Form = form,
                Services = services,
                Mine = await _bookings.ListByClientAsync(ClientIdValue, User.Uid(), ClientEmailValue)
            });
        }

        var profile = await _users.GetAsync(User.Uid());
        await _bookings.CreateAsync(new Booking
        {
            ClientId = ClientIdValue,
            Uid = User.Uid(),
            ClientName = profile?.FullName ?? User.Identity?.Name ?? "",
            ClientEmail = profile?.Email ?? ClientEmailValue,
            ClientPhone = profile?.Phone ?? "",
            EventType = service!.Name,
            Date = dateIso,
            StartTime = form.StartTime,
            EndTime = form.EndTime,
            Hours = hours!.Value,
            Location = form.Location.Trim(),
            Notes = (form.Notes ?? "").Trim(),
            Status = BookingStatus.Pending,
            EstimatedTotal = service.PriceFor(hours.Value)
        });

        TempData["BookingMessage"] = "Your booking request has been submitted to the photographer for review!";
        return RedirectToAction(nameof(Bookings));
    }

    [HttpPost]
    public async Task<IActionResult> CancelBooking(string id)
    {
        var b = await _bookings.GetAsync(id);
        bool isOwner = b is not null && (
            (!string.IsNullOrWhiteSpace(ClientIdValue) && b.ClientId.Equals(ClientIdValue, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(User.Uid()) && b.Uid.Equals(User.Uid(), StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(ClientEmailValue) && b.ClientEmail.Equals(ClientEmailValue, StringComparison.OrdinalIgnoreCase))
        );
        if (b is not null && isOwner
            && (BookingStatus.IsPending(b.Status) || BookingStatus.IsConfirmed(b.Status)))
        {
            await _bookings.SetStatusAsync(id, BookingStatus.Cancelled);
            TempData["BookingMessage"] = "Your booking request has been cancelled.";
        }
        return RedirectToAction(nameof(Bookings));
    }

    // ---------- Invoices ----------
    public async Task<IActionResult> Invoices()
    {
        var profile = await _users.GetAsync(User.Uid());
        var list = await _invoices.ListByClientAsync(ClientIdValue);
        return View(list.Select(i => new InvoiceSheetModel
        {
            Invoice = i,
            ClientName = profile?.FullName ?? "",
            ClientEmail = profile?.Email ?? "",
            ClientPhone = profile?.Phone ?? ""
        }).ToList());
    }

    // ---------- Galleries ----------
    private async Task<(bool IsPaid, string? InvoiceNo, string? InvoiceStatus)> CheckGalleryPaymentAsync(Gallery g, List<Invoice> clientInvoices)
    {
        if (!string.IsNullOrEmpty(g.BookingId))
        {
            var booking = await _bookings.GetAsync(g.BookingId);
            if (booking != null)
            {
                if (booking.EstimatedTotal == 0) return (true, null, "Subsidized");

                var inv = clientInvoices.FirstOrDefault(i => i.BookingId == g.BookingId);
                if (inv != null)
                {
                    bool paid = inv.Status.Equals(InvoiceStatus.Paid, StringComparison.OrdinalIgnoreCase);
                    return (paid, inv.InvoiceNo, inv.Status);
                }

                if (!string.IsNullOrEmpty(booking.InvoiceStatus))
                {
                    bool paid = booking.InvoiceStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase);
                    return (paid, booking.InvoiceNumber, booking.InvoiceStatus);
                }
            }
        }

        var matchingInvoice = clientInvoices.FirstOrDefault(i =>
            !string.IsNullOrEmpty(i.Service) && g.Title.Contains(i.Service, StringComparison.OrdinalIgnoreCase));
        if (matchingInvoice != null)
        {
            bool paid = matchingInvoice.Status.Equals(InvoiceStatus.Paid, StringComparison.OrdinalIgnoreCase);
            return (paid, matchingInvoice.InvoiceNo, matchingInvoice.Status);
        }

        var unpaid = clientInvoices.FirstOrDefault(i => !i.Status.Equals(InvoiceStatus.Paid, StringComparison.OrdinalIgnoreCase));
        if (unpaid != null)
        {
            return (false, unpaid.InvoiceNo, unpaid.Status);
        }

        return (true, null, "Paid");
    }

    public async Task<IActionResult> Gallery()
    {
        var list = await _galleries.ListByClientAsync(ClientIdValue);
        var invoices = await _invoices.ListByClientAsync(ClientIdValue);

        var cards = new List<GalleryCardViewModel>();
        foreach (var g in list)
        {
            var (isPaid, invNo, invStatus) = await CheckGalleryPaymentAsync(g, invoices);
            cards.Add(new GalleryCardViewModel
            {
                Gallery = g,
                CoverUrl = g.CoverPath is null ? null : _galleries.SignedUrl(g.CoverPath),
                IsPaid = isPaid,
                InvoiceNo = invNo,
                InvoiceStatus = invStatus
            });
        }
        return View(cards);
    }

    public async Task<IActionResult> Album(string id)
    {
        var g = await _galleries.GetAsync(id);
        if (g is null || g.ClientId != ClientIdValue) return NotFound();

        var invoices = await _invoices.ListByClientAsync(ClientIdValue);
        var (isPaid, invNo, _) = await CheckGalleryPaymentAsync(g, invoices);
        if (!isPaid)
        {
            TempData["ClientError"] = $"Payment is required to view your album photos. Please settle invoice {invNo ?? ""} to unlock your gallery.";
            return RedirectToAction(nameof(Invoices));
        }

        return View(new AlbumViewModel
        {
            Gallery = g,
            PhotoUrls = g.PhotoPaths.Select(p => _galleries.SignedUrl(p)).ToList()
        });
    }

    public async Task<IActionResult> DownloadAlbum(string id, CancellationToken ct)
    {
        var g = await _galleries.GetAsync(id);
        if (g is null || g.ClientId != ClientIdValue) return NotFound();

        var invoices = await _invoices.ListByClientAsync(ClientIdValue);
        var (isPaid, invNo, _) = await CheckGalleryPaymentAsync(g, invoices);
        if (!isPaid)
        {
            TempData["ClientError"] = $"Payment is required to download this album. Please settle invoice {invNo ?? ""} to unlock your photos.";
            return RedirectToAction(nameof(Invoices));
        }

        var bodyControl = HttpContext.Features.Get<IHttpBodyControlFeature>();
        if (bodyControl is not null) bodyControl.AllowSynchronousIO = true;

        Response.ContentType = "application/zip";
        Response.Headers.ContentDisposition = $"attachment; filename=\"{Format.SafeFileName(g.Title)}.zip\"";
        await _galleries.WriteZipAsync(g, Response.Body, ct);
        return new EmptyResult();
    }

    // ---------- Reviews ----------
    [HttpGet]
    public async Task<IActionResult> Reviews() => View(await _reviews.ListByClientAsync(ClientIdValue));

    [HttpPost]
    public async Task<IActionResult> Reviews(int rating, string? review)
    {
        review = (review ?? "").Trim();
        if (rating < 1 || rating > 5) ModelState.AddModelError("", "Please choose a rating from 1 to 5 stars.");
        if (review.Length == 0) ModelState.AddModelError("", "Please enter your review comments.");
        if (review.Length > 2000) ModelState.AddModelError("", "Review is too long (maximum 2000 characters).");

        if (!ModelState.IsValid) return View(await _reviews.ListByClientAsync(ClientIdValue));

        var profile = await _users.GetAsync(User.Uid());
        await _reviews.CreateAsync(new Review
        {
            ClientId = ClientIdValue,
            ClientName = profile?.FullName ?? "",
            Rating = rating,
            Text = review,
            Approved = true
        });
        TempData["ReviewMessage"] = "Thank you! Your feedback has been recorded.";
        return RedirectToAction(nameof(Reviews));
    }

    // ---------- Profile ----------
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var profile = await _users.GetAsync(User.Uid());
        return profile is null ? RedirectToAction("Login", "Account") : View(profile);
    }

    [HttpPost]
    public async Task<IActionResult> Profile(string fullName, string? phone)
    {
        fullName = (fullName ?? "").Trim();
        phone = (phone ?? "").Trim();
        if (fullName.Length == 0 || fullName.Length > 100 || phone.Length > 30)
        {
            TempData["ProfileError"] = "Please enter a valid full name and phone number.";
            return RedirectToAction(nameof(Profile));
        }
        await _users.UpdateContactAsync(User.Uid(), fullName, phone);
        TempData["ProfileMessage"] = "Profile details updated.";
        return RedirectToAction(nameof(Profile));
    }
}
