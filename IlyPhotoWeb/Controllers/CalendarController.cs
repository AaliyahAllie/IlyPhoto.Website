using IlyPhotoWeb.Models;
using IlyPhotoWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IlyPhotoWeb.Controllers;

/// <summary>
/// API endpoint for multi-month interactive calendars.
/// Returns status for each day of the month: "busy", "mine", "blocked", or "available".
/// </summary>
[Route("api/calendar")]
public class CalendarController : Controller
{
    private readonly BookingStore _bookings;
    public CalendarController(BookingStore bookings) => _bookings = bookings;

    [HttpGet]
    public async Task<IActionResult> Get(int year, int month)
    {
        if (year < 2020 || year > 2100 || month < 1 || month > 12) return BadRequest();

        var first = new DateTime(year, month, 1);
        var from = Format.Iso(first);
        var to = Format.Iso(first.AddMonths(1).AddDays(-1));
        var isAdmin = User.IsInRole(Roles.Admin);
        var uid = User.Identity?.IsAuthenticated == true ? User.Uid() : "";
        var cid = User.Identity?.IsAuthenticated == true ? User.ClientId() : "";
        var result = new Dictionary<string, string>();

        foreach (var b in await _bookings.ListByDateRangeAsync(from, to))
        {
            if (!Format.TryParseDate(b.Date, out var eventDt)) continue;
            var day = eventDt.Day.ToString();
            var mine = !isAdmin && (
                (!string.IsNullOrWhiteSpace(uid) && b.Uid.Equals(uid, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(cid) && b.ClientId.Equals(cid, StringComparison.OrdinalIgnoreCase))
            );
            bool isConfirmed = BookingStatus.IsConfirmed(b.Status);
            bool isPending = BookingStatus.IsPending(b.Status);

            if (isConfirmed)
                result[day] = mine ? "mine" : "busy";
            else if (isPending && mine && !result.ContainsKey(day))
                result[day] = "mine";
        }

        foreach (var blocked in await _bookings.ListBlockedAsync(from, to))
        {
            if (Format.TryParseDate(blocked.Date, out var dt))
            {
                result[dt.Day.ToString()] = "blocked";
            }
        }

        return Json(result);
    }
}
