using System.Globalization;
using System.Security.Claims;

namespace IlyPhotoWeb.Models;

public static class Format
{
    private static readonly CultureInfo Za = new("en-ZA");

    public static string Rand(double amount) =>
        "R" + (Math.Abs(amount % 1) < 0.005 ? amount.ToString("N0", Za) : amount.ToString("N2", Za));

    public static string Rand(decimal amount) =>
        Rand((double)amount);

    private static readonly string[] SupportedDateFormats =
    {
        "yyyy-MM-dd",
        "d MMMM yyyy",
        "dd MMMM yyyy",
        "d MMM yyyy",
        "dd MMM yyyy",
        "MMMM d, yyyy",
        "MMM d, yyyy",
        "yyyy/MM/dd",
        "dd/MM/yyyy",
        "d/M/yyyy",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ssZ",
        "yyyy-MM-ddTHH:mm:ss.fffffffZ"
    };

    public static string Date(string? raw) =>
        TryParseDate(raw, out var d)
            ? d.ToString("d MMM yyyy", CultureInfo.InvariantCulture)
            : (raw ?? "");

    public static string Iso(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string NormalizeDateIso(string? raw) =>
        TryParseDate(raw, out var d) ? Iso(d) : (raw ?? "").Trim();

    /// <summary>Today's date in South Africa, regardless of server location.</summary>
    public static DateTime TodaySA()
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date;
        }
        catch
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("Africa/Johannesburg");
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date;
            }
            catch
            {
                return DateTime.Today;
            }
        }
    }

    public static string TodayIso() => Iso(TodaySA());

    public static string StatusClass(string status) => status switch
    {
        "Confirmed" or "Paid" => "confirmed",
        "Pending" or "Partially Paid" => "pending",
        "Unpaid" or "Overdue" => "unpaid",
        _ => "muted"
    };

    public static bool TryParseMoney(string? text, out double value) =>
        double.TryParse((text ?? "").Trim().Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        && value >= 0 && !double.IsInfinity(value);

    public static bool TryParseDate(string? text, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var clean = text.Trim();

        if (DateTime.TryParseExact(clean, SupportedDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out date))
        {
            date = date.Date;
            return true;
        }

        if (DateTime.TryParse(clean, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out date) ||
            DateTime.TryParse(clean, Za, DateTimeStyles.AssumeLocal, out date))
        {
            date = date.Date;
            return true;
        }

        return false;
    }

    public static bool TryParseTime(string? text, out TimeSpan time) =>
        TimeSpan.TryParseExact(text, @"hh\:mm", CultureInfo.InvariantCulture, out time);

    /// <summary>Whole hours between two HH:mm times, rounded up (minimum 1). Null if end is not after start.</summary>
    public static int? HoursBetween(string start, string end)
    {
        if (!TryParseTime(start, out var s) || !TryParseTime(end, out var e) || e <= s) return null;
        return Math.Max(1, (int)Math.Ceiling((e - s).TotalMinutes / 60.0));
    }

    public static string SafeFileName(string name)
    {
        var clean = new string(Path.GetFileName(name).Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_').ToArray());
        return string.IsNullOrWhiteSpace(clean) ? "photo" : clean;
    }
}

public static class ClaimsExtensions
{
    public static string Uid(this ClaimsPrincipal u) => u.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    public static string ClientId(this ClaimsPrincipal u) => u.FindFirstValue("ClientId") ?? "";
}
