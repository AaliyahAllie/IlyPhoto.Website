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
        var result = new Dictionary<string, string>();

        foreach (var b in await _bookings.ListByDateRangeAsync(from, to))
        {
            var day = b.EventDate.Day.ToString();
            var mine = !isAdmin && b.Uid == uid;
            if (b.Status == BookingStatus.Confirmed)
                result[day] = mine ? "mine" : "busy";
            else if (b.Status == BookingStatus.Pending && mine && !result.ContainsKey(day))
                result[day] = "mine";
        }

        foreach (var blocked in await _bookings.ListBlockedAsync(from, to))
        {
            if (DateTime.TryParseExact(blocked.Date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dt))
            {
                result[dt.Day.ToString()] = "blocked";
            }
        }

        return Json(result);
    }
}
