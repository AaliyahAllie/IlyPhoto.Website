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

    // ---------- Dashboard ----------
    public async Task<IActionResult> Index()
    {
        var today = Format.TodayIso();
        var bookings = await _bookings.ListByClientAsync(ClientIdValue);
        var invoices = await _invoices.ListByClientAsync(ClientIdValue);
        var galleries = await _galleries.ListByClientAsync(ClientIdValue);

        var upcoming = bookings
            .Where(b => (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed)
                        && string.CompareOrdinal(b.Date, today) >= 0)
            .OrderBy(b => b.Date).ToList();
        var outstanding = invoices.Where(i => i.Status != InvoiceStatus.Paid).ToList();

        return View(new ClientDashboardViewModel
        {
            Next = upcoming.FirstOrDefault(),
            UpcomingCount = upcoming.Count,
            OutstandingCount = outstanding.Count,
            OutstandingTotal = outstanding.Sum(i => i.Total),
            GalleryCount = galleries.Count(g => g.PhotoCount > 0)
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
            Mine = await _bookings.ListByClientAsync(ClientIdValue)
        });
    }

    [HttpPost]
    public async Task<IActionResult> Bookings(BookingViewModel form)
    {
        var services = await _services.ListAsync();
        var service = services.FirstOrDefault(s => s.Name == form.EventType);
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
                Mine = await _bookings.ListByClientAsync(ClientIdValue)
            });
        }

        var profile = await _users.GetAsync(User.Uid());
        await _bookings.CreateAsync(new Booking
        {
            ClientId = ClientIdValue,
            Uid = User.Uid(),
            ClientName = profile?.FullName ?? User.Identity?.Name ?? "",
            ClientEmail = profile?.Email ?? "",
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
        if (b is not null && b.ClientId == ClientIdValue
            && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed))
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
    public async Task<IActionResult> Gallery()
    {
        var list = await _galleries.ListByClientAsync(ClientIdValue);
        return View(list.Select(g => new GalleryCardViewModel
        {
            Gallery = g,
            CoverUrl = g.CoverPath is null ? null : _galleries.SignedUrl(g.CoverPath)
        }).ToList());
    }

    public async Task<IActionResult> Album(string id)
    {
        var g = await _galleries.GetAsync(id);
        if (g is null || g.ClientId != ClientIdValue) return NotFound();
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
