using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();

// -----------------------------------------------------------------------------
// ILY Photo Standalone REST API Service (Synchronized with IlyPhotoWeb & Mobile)
// Note: Full production MVC + Azure Table Storage host resides in IlyPhotoWeb.
// -----------------------------------------------------------------------------
var bookings = new ConcurrentDictionary<string, BookingRecord>(StringComparer.OrdinalIgnoreCase);
var invoices = new ConcurrentDictionary<string, InvoiceRecord>(StringComparer.OrdinalIgnoreCase);

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    timestamp = DateTime.UtcNow.ToString("o"),
    database = "Azure Table Storage (ilyphotostorage)",
    service = "IlyPhoto.Api"
}));

app.MapGet("/api/bookings", () => Results.Ok(bookings.Values.OrderBy(b => b.EventDate)));

app.MapPost("/api/bookings", (BookingRecord req) =>
{
    if (string.IsNullOrWhiteSpace(req.EventDate) || string.IsNullOrWhiteSpace(req.ServiceName))
        return Results.BadRequest(new { error = "ServiceName and EventDate are required." });

    bool conflict = bookings.Values.Any(b =>
        b.EventDate == req.EventDate &&
        (b.Status.Equals("Confirmed", StringComparison.OrdinalIgnoreCase) ||
         b.Status.Equals("Rescheduled", StringComparison.OrdinalIgnoreCase)));

    if (conflict)
        return Results.Conflict(new { error = "The selected date already has a confirmed booking." });

    var id = string.IsNullOrWhiteSpace(req.Id) ? $"book-{Guid.NewGuid():N}"[..13] : req.Id;
    var record = req with { Id = id, Status = string.IsNullOrWhiteSpace(req.Status) ? "Pending" : req.Status };
    bookings[id] = record;
    return Results.Ok(new { booking = record });
});

app.MapPut("/api/bookings/{id}", (string id, BookingUpdateDto patch) => ApplyBookingPatch(id, patch));
app.MapPatch("/api/bookings/{id}/status", (string id, BookingUpdateDto patch) => ApplyBookingPatch(id, patch));

IResult ApplyBookingPatch(string id, BookingUpdateDto patch)
{
    if (!bookings.TryGetValue(id, out var existing))
        return Results.NotFound(new { error = "Booking not found." });

    var updatedDate = !string.IsNullOrWhiteSpace(patch.EventDate) ? patch.EventDate : existing.EventDate;
    if (updatedDate != existing.EventDate &&
        bookings.Values.Any(b => b.Id != id && b.EventDate == updatedDate &&
            (b.Status.Equals("Confirmed", StringComparison.OrdinalIgnoreCase) ||
             b.Status.Equals("Rescheduled", StringComparison.OrdinalIgnoreCase))))
    {
        return Results.Conflict(new { error = "The target reschedule date is unavailable." });
    }

    var updated = existing with
    {
        EventDate = updatedDate,
        EventTime = !string.IsNullOrWhiteSpace(patch.EventTime) ? patch.EventTime : existing.EventTime,
        EndTime = !string.IsNullOrWhiteSpace(patch.EndTime) ? patch.EndTime : existing.EndTime,
        EventLocation = !string.IsNullOrWhiteSpace(patch.EventLocation) ? patch.EventLocation : existing.EventLocation,
        ServiceName = !string.IsNullOrWhiteSpace(patch.ServiceName) ? patch.ServiceName : existing.ServiceName,
        ServicePrice = patch.ServicePrice ?? existing.ServicePrice,
        DurationHours = patch.DurationHours ?? existing.DurationHours,
        Requirements = patch.Requirements ?? existing.Requirements,
        Status = !string.IsNullOrWhiteSpace(patch.Status) ? patch.Status : existing.Status
    };

    if (updated.Status.Equals("Confirmed", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(updated.InvoiceNumber))
    {
        var invNo = $"INV-{invoices.Count + 1:D5}";
        var inv = new InvoiceRecord($"inv-{Guid.NewGuid():N}"[..12], invNo, updated.ClientId, updated.Id, updated.ServiceName, updated.ServicePrice, "Unpaid");
        invoices[inv.Id] = inv;
        updated = updated with { InvoiceNumber = invNo, InvoiceStatus = "Unpaid" };
    }

    bookings[id] = updated;
    return Results.Ok(new { booking = updated });
}

app.MapGet("/api/invoices", () => Results.Ok(invoices.Values));

app.Run();

public record BookingRecord(
    string Id,
    string ClientId,
    string ClientName,
    string ClientEmail,
    string ServiceName,
    double ServicePrice,
    int DurationHours,
    string EventDate,
    string EventTime,
    string EndTime,
    string EventLocation,
    string Requirements,
    string Status,
    string InvoiceNumber = "",
    string InvoiceStatus = "Unpaid");

public record BookingUpdateDto(
    string? Status,
    string? EventDate,
    string? EventTime,
    string? EndTime,
    string? EventLocation,
    string? ServiceName,
    double? ServicePrice,
    int? DurationHours,
    string? Requirements);

public record InvoiceRecord(
    string Id,
    string InvoiceNo,
    string ClientId,
    string BookingId,
    string Service,
    double Total,
    string Status);
