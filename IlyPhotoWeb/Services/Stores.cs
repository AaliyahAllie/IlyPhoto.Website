using System.Globalization;
using Azure;
using Azure.Data.Tables;
using IlyPhotoWeb.Models;

namespace IlyPhotoWeb.Services;

public class CounterStore
{
    private readonly AzureTableContext _context;
    private const string TableName = "counters";

    public CounterStore(AzureTableContext context) => _context = context;

    /// <summary>Atomically increments counters/{name} and returns the new value.</summary>
    public async Task<long> NextAsync(string name, long start)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Counter name is required.", nameof(name));

        var entity = await _context.GetEntityOrNullAsync(TableName, "counter", name);
        long current = start;
        if (entity != null && entity.TryGetValue("Value", out var v))
        {
            current = v is long l ? l : Convert.ToInt64(v);
        }
        long next = current + 1;
        var newEntity = new TableEntity("counter", name) { { "Value", next } };
        await _context.UpsertAsync(TableName, newEntity, TableUpdateMode.Replace);
        return next;
    }
}

public class UserStore
{
    private readonly AzureTableContext _context;
    private readonly AzureCloudSyncService? _cloudSync;
    private const string TableName = "users";

    public UserStore(AzureTableContext context, AzureCloudSyncService? cloudSync = null)
    {
        _context = context;
        _cloudSync = cloudSync;
    }

    public async Task<UserProfile?> GetAsync(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid)) return null;
        if (_cloudSync != null) await _cloudSync.EnsureSyncedAsync();
        var entity = await _context.GetEntityOrNullAsync(TableName, "user", uid);
        return entity != null ? ToUserProfile(entity) : null;
    }

    public async Task<UserProfile?> GetByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        if (_cloudSync != null) await _cloudSync.EnsureSyncedAsync();
        var cleanEmail = email.Trim().ToLowerInvariant();
        var entities = await _context.QueryAllEntitiesAsync(TableName);
        foreach (var entity in entities)
        {
            var e = entity.GetString("Email") ?? entity.GetString("email") ?? "";
            if (e.Equals(cleanEmail, StringComparison.OrdinalIgnoreCase))
            {
                return ToUserProfile(entity);
            }
        }
        return null;
    }

    public async Task<UserProfile?> GetByClientIdAsync(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId)) return null;
        if (_cloudSync != null) await _cloudSync.EnsureSyncedAsync();
        var entities = await _context.QueryAllEntitiesAsync(TableName);
        foreach (var entity in entities)
        {
            var cid = entity.GetString("ClientId") ?? entity.GetString("clientId") ?? "";
            var uid = entity.GetString("Id") ?? entity.GetString("id") ?? entity.GetString("uid") ?? entity.RowKey;
            var em = entity.GetString("Email") ?? entity.GetString("email") ?? "";
            if (cid.Equals(clientId, StringComparison.OrdinalIgnoreCase) ||
                uid.Equals(clientId, StringComparison.OrdinalIgnoreCase) ||
                em.Equals(clientId, StringComparison.OrdinalIgnoreCase))
            {
                return ToUserProfile(entity);
            }
        }
        return null;
    }

    public async Task<List<UserProfile>> ListClientsAsync()
    {
        if (_cloudSync != null) await _cloudSync.EnsureSyncedAsync();
        var list = new List<UserProfile>();
        var entities = await _context.QueryAllEntitiesAsync(TableName);
        foreach (var entity in entities)
        {
            var role = entity.GetString("Role") ?? entity.GetString("role") ?? "";
            if (role.Equals(Roles.Client, StringComparison.OrdinalIgnoreCase) || role.Equals("client", StringComparison.OrdinalIgnoreCase))
            {
                list.Add(ToUserProfile(entity));
            }
        }
        return list.OrderBy(u => u.ClientId, StringComparer.Ordinal).ToList();
    }

    public async Task SaveAsync(UserProfile p)
    {
        if (string.IsNullOrWhiteSpace(p.Email))
            throw new ArgumentException("User email is required.", nameof(p));

        if (string.IsNullOrWhiteSpace(p.Id)) p.Id = Guid.NewGuid().ToString("N");
        var entity = new TableEntity("user", p.Id)
        {
            { "Id", p.Id },
            { "id", p.Id },
            { "uid", p.Id },
            { "ClientId", p.ClientId },
            { "clientId", p.ClientId },
            { "FullName", p.FullName },
            { "displayName", p.FullName },
            { "Email", p.Email.ToLowerInvariant().Trim() },
            { "email", p.Email.ToLowerInvariant().Trim() },
            { "Phone", p.Phone },
            { "phone", p.Phone },
            { "Address", p.Address },
            { "address", p.Address },
            { "PhotoUrl", p.PhotoUrl },
            { "photoUrl", p.PhotoUrl },
            { "Role", p.Role },
            { "role", p.Role.ToLowerInvariant() },
            { "PasswordHash", p.PasswordHash },
            { "passwordHash", p.PasswordHash },
            { "CreatedAt", p.CreatedAt.ToString() }
        };
        await _context.UpsertAsync(TableName, entity, TableUpdateMode.Merge);
    }

    public async Task UpdateContactAsync(string uid, string fullName, string phone)
    {
        var user = await GetAsync(uid) ?? throw new InvalidOperationException($"User '{uid}' not found.");
        user.FullName = fullName;
        user.Phone = phone;
        await SaveAsync(user);
    }

    public async Task UpdateRoleAsync(string uid, string role, string clientId)
    {
        var user = await GetAsync(uid) ?? throw new InvalidOperationException($"User '{uid}' not found.");
        user.Role = role;
        user.ClientId = clientId;
        await SaveAsync(user);
    }

    private static UserProfile ToUserProfile(TableEntity e)
    {
        var createdAtStr = e.GetString("CreatedAt") ?? e.GetString("createdAt");
        DateTime dt = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(createdAtStr)) DateTime.TryParse(createdAtStr, out dt);

        return new UserProfile
        {
            Id = e.GetString("Id") ?? e.GetString("id") ?? e.GetString("uid") ?? e.RowKey,
            ClientId = e.GetString("ClientId") ?? e.GetString("clientId") ?? "",
            FullName = e.GetString("FullName") ?? e.GetString("displayName") ?? "",
            Email = e.GetString("Email") ?? e.GetString("email") ?? "",
            Phone = e.GetString("Phone") ?? e.GetString("phone") ?? "",
            Address = e.GetString("Address") ?? e.GetString("address") ?? "",
            PhotoUrl = e.GetString("PhotoUrl") ?? e.GetString("photoUrl") ?? "",
            Role = NormalizeRole(e.GetString("Role") ?? e.GetString("role")),
            PasswordHash = e.GetString("PasswordHash") ?? e.GetString("passwordHash") ?? "",
            CreatedAt = new Timestamp(dt)
        };
    }

    private static string NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return Roles.Client;
        if (role.Equals("admin", StringComparison.OrdinalIgnoreCase)) return Roles.Admin;
        if (role.Equals("photographer", StringComparison.OrdinalIgnoreCase)) return Roles.Admin;
        return Roles.Client;
    }
}

public class BookingStore
{
    private readonly AzureTableContext _context;
    private readonly AzureCloudSyncService? _cloudSync;
    private const string TableName = "bookings";
    private const string BlockedTableName = "blockedDates";

    public BookingStore(AzureTableContext context, AzureCloudSyncService? cloudSync = null)
    {
        _context = context;
        _cloudSync = cloudSync;
    }

    public async Task<Booking?> GetAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (_cloudSync != null) await _cloudSync.EnsureSyncedAsync();
        var entity = await _context.GetEntityOrNullAsync(TableName, "booking", id);
        return entity != null ? ToBooking(entity) : null;
    }

    public async Task<string> CreateAsync(Booking b)
    {
        if (string.IsNullOrWhiteSpace(b.Date))
            throw new ArgumentException("Booking date is required.", nameof(b));
        if (string.IsNullOrWhiteSpace(b.EventType))
            throw new ArgumentException("Booking service/event type is required.", nameof(b));

        b.Date = Format.NormalizeDateIso(b.Date);
        bool isExisting = !string.IsNullOrWhiteSpace(b.Id) &&
                          (await _context.GetEntityOrNullAsync(TableName, "booking", b.Id)) != null;

        if (string.IsNullOrWhiteSpace(b.Id)) b.Id = "book-" + Guid.NewGuid().ToString("N")[..8];

        if (!isExisting && _cloudSync != null && _cloudSync.IsEnabled)
        {
            var remoteId = await _cloudSync.PushCreateBookingAsync(b);
            if (!string.IsNullOrWhiteSpace(remoteId))
            {
                b.Id = remoteId;
            }
        }

        await SaveEntityLocalAsync(b);

        if (isExisting && _cloudSync != null && _cloudSync.IsEnabled)
        {
            await _cloudSync.PushUpdateBookingAsync(b);
        }

        return b.Id;
    }

    public async Task<string> UpdateBookingAsync(Booking b)
    {
        if (string.IsNullOrWhiteSpace(b.Date))
            throw new ArgumentException("Booking date is required.", nameof(b));
        if (string.IsNullOrWhiteSpace(b.EventType))
            throw new ArgumentException("Booking service/event type is required.", nameof(b));

        b.Date = Format.NormalizeDateIso(b.Date);
        if (string.IsNullOrWhiteSpace(b.Id)) b.Id = "book-" + Guid.NewGuid().ToString("N")[..8];

        await SaveEntityLocalAsync(b);

        if (_cloudSync != null && _cloudSync.IsEnabled)
        {
            await _cloudSync.PushUpdateBookingAsync(b);
        }

        return b.Id;
    }

    private Task SaveEntityLocalAsync(Booking b)
    {
        var entity = new TableEntity("booking", b.Id)
        {
            { "Id", b.Id },
            { "id", b.Id },
            { "ClientId", b.ClientId },
            { "clientId", b.ClientId },
            { "Uid", b.Uid },
            { "uid", b.Uid },
            { "ClientName", b.ClientName },
            { "clientName", b.ClientName },
            { "ClientEmail", b.ClientEmail },
            { "clientEmail", b.ClientEmail },
            { "ClientPhone", b.ClientPhone },
            { "clientPhone", b.ClientPhone },
            { "EventType", b.EventType },
            { "serviceName", b.EventType },
            { "Date", b.Date },
            { "eventDate", b.Date },
            { "StartTime", b.StartTime },
            { "eventTime", b.StartTime },
            { "EndTime", b.EndTime },
            { "endTime", b.EndTime },
            { "Hours", b.Hours },
            { "durationHours", b.Hours },
            { "Location", b.Location },
            { "eventLocation", b.Location },
            { "Notes", b.Notes },
            { "requirements", b.Notes },
            { "Status", b.Status },
            { "status", b.Status },
            { "EstimatedTotal", b.EstimatedTotal },
            { "servicePrice", b.EstimatedTotal },
            { "InvoiceNumber", b.InvoiceNumber },
            { "invoiceNumber", b.InvoiceNumber },
            { "InvoiceStatus", b.InvoiceStatus },
            { "invoiceStatus", b.InvoiceStatus },
            { "CreatedAt", b.CreatedAt.ToString() }
        };
        return _context.UpsertAsync(TableName, entity, TableUpdateMode.Replace);
    }

    public async Task<List<Booking>> ListAllAsync()
    {
        if (_cloudSync != null) await _cloudSync.EnsureSyncedAsync();
        var list = new List<Booking>();
        var entities = await _context.QueryAllEntitiesAsync(TableName);
        foreach (var entity in entities)
        {
            list.Add(ToBooking(entity));
        }
        return list.OrderBy(b => b.Date).ThenBy(b => b.StartTime).ToList();
    }

    public async Task<List<Booking>> ListByClientAsync(string clientId, string? email = null, string? uid = null, string? fullName = null)
    {
        var all = await ListAllAsync();
        return all
            .Where(b => b.BelongsToClient(clientId, email, uid, fullName))
            .OrderByDescending(b => b.Date)
            .ToList();
    }

    public async Task<List<Booking>> ListByDateRangeAsync(string fromIso, string toIso)
    {
        var normFrom = Format.NormalizeDateIso(fromIso);
        var normTo = Format.NormalizeDateIso(toIso);
        var all = await ListAllAsync();
        return all.Where(b => string.Compare(Format.NormalizeDateIso(b.Date), normFrom, StringComparison.Ordinal) >= 0 &&
                              string.Compare(Format.NormalizeDateIso(b.Date), normTo, StringComparison.Ordinal) <= 0).ToList();
    }

    public async Task<bool> HasConfirmedOnDateAsync(string dateIso, string? exceptBookingId = null)
    {
        var normDate = Format.NormalizeDateIso(dateIso);
        var all = await ListAllAsync();
        return all.Any(b => Format.NormalizeDateIso(b.Date) == normDate &&
            (string.Equals(b.Status, BookingStatus.Confirmed, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(b.Status, BookingStatus.Rescheduled, StringComparison.OrdinalIgnoreCase)) &&
            !string.Equals(b.Id, exceptBookingId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> HasConflictOnDateAsync(string dateIso, string? exceptBookingId = null)
    {
        var normDate = Format.NormalizeDateIso(dateIso);
        if (await IsBlockedAsync(normDate)) return true;
        return await HasConfirmedOnDateAsync(normDate, exceptBookingId);
    }

    public async Task SetStatusAsync(string id, string status)
    {
        var b = await GetAsync(id) ?? throw new InvalidOperationException($"Booking '{id}' not found.");
        b.Status = status;
        await UpdateBookingAsync(b);
    }

    public async Task<Booking> RescheduleAsync(
        string id,
        string date,
        string start,
        string end,
        int hours,
        double total,
        string? location = null,
        string? notes = null,
        string? eventType = null,
        string? status = null)
    {
        var b = await GetAsync(id) ?? throw new InvalidOperationException($"Booking '{id}' not found.");
        b.Date = Format.NormalizeDateIso(date);
        b.StartTime = start;
        b.EndTime = end;
        b.Hours = hours;
        b.EstimatedTotal = total;
        if (!string.IsNullOrWhiteSpace(location)) b.Location = location.Trim();
        if (notes != null) b.Notes = notes.Trim();
        if (!string.IsNullOrWhiteSpace(eventType)) b.EventType = eventType.Trim();
        if (!string.IsNullOrWhiteSpace(status)) b.Status = status.Trim();
        await UpdateBookingAsync(b);
        return b;
    }

    // ---- blocked dates ----
    public async Task<bool> IsBlockedAsync(string dateIso)
    {
        if (string.IsNullOrWhiteSpace(dateIso)) return false;
        var norm = Format.NormalizeDateIso(dateIso);
        var entity = await _context.GetEntityOrNullAsync(BlockedTableName, "blockedDate", norm);
        return entity != null;
    }

    public async Task<List<BlockedDate>> ListBlockedAsync(string fromIso, string toIso)
    {
        var normFrom = Format.NormalizeDateIso(fromIso);
        var normTo = Format.NormalizeDateIso(toIso);
        var list = new List<BlockedDate>();
        var entities = await _context.QueryAllEntitiesAsync(BlockedTableName);
        foreach (var entity in entities)
        {
            var d = Format.NormalizeDateIso(entity.GetString("Date") ?? entity.RowKey);
            if (string.Compare(d, normFrom, StringComparison.Ordinal) >= 0 &&
                string.Compare(d, normTo, StringComparison.Ordinal) <= 0)
            {
                list.Add(new BlockedDate
                {
                    Id = entity.RowKey,
                    Date = d,
                    Reason = entity.GetString("Reason") ?? ""
                });
            }
        }
        return list.OrderBy(b => b.Date).ToList();
    }

    public async Task BlockAsync(string dateIso, string reason)
    {
        if (string.IsNullOrWhiteSpace(dateIso))
            throw new ArgumentException("Date is required.", nameof(dateIso));

        var norm = Format.NormalizeDateIso(dateIso);
        var entity = new TableEntity("blockedDate", norm)
        {
            { "Date", norm },
            { "Reason", reason },
            { "CreatedAt", DateTime.UtcNow.ToString("o") }
        };
        await _context.UpsertAsync(BlockedTableName, entity, TableUpdateMode.Replace);
    }

    public async Task UnblockAsync(string dateIso)
    {
        if (string.IsNullOrWhiteSpace(dateIso))
            throw new ArgumentException("Date is required.", nameof(dateIso));
        var norm = Format.NormalizeDateIso(dateIso);
        await _context.DeleteAsync(BlockedTableName, "blockedDate", norm);
    }

    private static Booking ToBooking(TableEntity e)
    {
        var createdAtStr = e.GetString("CreatedAt") ?? e.GetString("createdAt");
        DateTime dt = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(createdAtStr)) DateTime.TryParse(createdAtStr, out dt);

        double total = 0;
        if (e.TryGetValue("EstimatedTotal", out var et)) total = Convert.ToDouble(et);
        else if (e.TryGetValue("servicePrice", out var sp)) total = Convert.ToDouble(sp);

        int hours = 1;
        if (e.TryGetValue("Hours", out var h)) hours = Convert.ToInt32(h);
        else if (e.TryGetValue("durationHours", out var dh)) hours = Convert.ToInt32(dh);

        var rawDate = e.GetString("Date") ?? e.GetString("eventDate") ?? "";

        return new Booking
        {
            Id = e.GetString("Id") ?? e.GetString("id") ?? e.RowKey,
            ClientId = e.GetString("ClientId") ?? e.GetString("clientId") ?? "",
            Uid = e.GetString("Uid") ?? e.GetString("uid") ?? "",
            ClientName = e.GetString("ClientName") ?? e.GetString("clientName") ?? "",
            ClientEmail = e.GetString("ClientEmail") ?? e.GetString("clientEmail") ?? "",
            ClientPhone = e.GetString("ClientPhone") ?? e.GetString("clientPhone") ?? "",
            EventType = e.GetString("EventType") ?? e.GetString("serviceName") ?? "",
            Date = Format.NormalizeDateIso(rawDate),
            StartTime = e.GetString("StartTime") ?? e.GetString("eventTime") ?? "",
            EndTime = e.GetString("EndTime") ?? e.GetString("endTime") ?? "",
            Hours = hours,
            Location = e.GetString("Location") ?? e.GetString("eventLocation") ?? "",
            Notes = e.GetString("Notes") ?? e.GetString("requirements") ?? "",
            Status = e.GetString("Status") ?? e.GetString("status") ?? BookingStatus.Pending,
            EstimatedTotal = total,
            InvoiceNumber = e.GetString("InvoiceNumber") ?? e.GetString("invoiceNumber") ?? "",
            InvoiceStatus = e.GetString("InvoiceStatus") ?? e.GetString("invoiceStatus") ?? "Unpaid",
            CreatedAt = new Timestamp(dt)
        };
    }
}

public class InvoiceStore
{
    private readonly AzureTableContext _context;
    private readonly CounterStore _counters;
    private readonly AzureCloudSyncService? _cloudSync;
    private const string TableName = "invoices";

    public InvoiceStore(AzureTableContext context, CounterStore counters, AzureCloudSyncService? cloudSync = null)
    {
        _context = context;
        _counters = counters;
        _cloudSync = cloudSync;
    }

    public async Task<Invoice?> GetAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (_cloudSync != null) await _cloudSync.EnsureSyncedAsync();
        var entity = await _context.GetEntityOrNullAsync(TableName, "invoice", id);
        return entity != null ? ToInvoice(entity) : null;
    }

    public async Task<Invoice> CreateAsync(Invoice inv)
    {
        if (string.IsNullOrWhiteSpace(inv.ClientId))
            throw new ArgumentException("Invoice ClientId is required.", nameof(inv));

        if (string.IsNullOrWhiteSpace(inv.Id)) inv.Id = "inv-" + Guid.NewGuid().ToString("N")[..8];
        if (string.IsNullOrWhiteSpace(inv.InvoiceNo))
        {
            var n = await _counters.NextAsync("invoices", 0);
            inv.InvoiceNo = $"INV-{n:D5}";
        }
        inv.Total = Math.Round(inv.Quantity * inv.Price, 2);

        var entity = new TableEntity("invoice", inv.Id)
        {
            { "Id", inv.Id },
            { "InvoiceNo", inv.InvoiceNo },
            { "ClientId", inv.ClientId },
            { "BookingId", inv.BookingId },
            { "BookingLabel", inv.BookingLabel },
            { "IssueDate", Format.NormalizeDateIso(inv.IssueDate) },
            { "DueDate", Format.NormalizeDateIso(inv.DueDate) },
            { "Service", inv.Service },
            { "Quantity", inv.Quantity },
            { "Price", inv.Price },
            { "Total", inv.Total },
            { "Status", inv.Status },
            { "EmailSent", inv.EmailSent },
            { "CreatedAt", inv.CreatedAt.ToString() }
        };
        await _context.UpsertAsync(TableName, entity, TableUpdateMode.Replace);
        return inv;
    }

    public async Task<List<Invoice>> ListAllAsync()
    {
        if (_cloudSync != null) await _cloudSync.EnsureSyncedAsync();
        var list = new List<Invoice>();
        var entities = await _context.QueryAllEntitiesAsync(TableName);
        foreach (var entity in entities)
        {
            list.Add(ToInvoice(entity));
        }
        return list.OrderByDescending(i => i.InvoiceNo, StringComparer.Ordinal).ToList();
    }

    public async Task<List<Invoice>> ListByClientAsync(string clientId, string? email = null, string? uid = null)
    {
        if (_cloudSync != null) await _cloudSync.EnsureSyncedAsync();
        var list = new List<Invoice>();
        var entities = await _context.QueryAllEntitiesAsync(TableName);
        foreach (var entity in entities)
        {
            var cid = entity.GetString("ClientId") ?? "";
            if ((!string.IsNullOrWhiteSpace(clientId) && string.Equals(cid, clientId, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(email) && string.Equals(cid, email.Trim(), StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(uid) && string.Equals(cid, uid, StringComparison.OrdinalIgnoreCase)))
            {
                list.Add(ToInvoice(entity));
            }
        }
        return list.OrderByDescending(i => i.InvoiceNo, StringComparer.Ordinal).ToList();
    }

    public async Task SetStatusAsync(string id, string status)
    {
        var entity = await _context.GetEntityOrNullAsync(TableName, "invoice", id)
            ?? throw new InvalidOperationException($"Invoice '{id}' not found.");
        entity["Status"] = status;
        await _context.UpsertAsync(TableName, entity, TableUpdateMode.Replace);
    }

    private static Invoice ToInvoice(TableEntity e)
    {
        var createdAtStr = e.GetString("CreatedAt");
        DateTime dt = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(createdAtStr)) DateTime.TryParse(createdAtStr, out dt);

        double price = 0, total = 0;
        int qty = 1;
        if (e.TryGetValue("Price", out var p)) price = Convert.ToDouble(p);
        if (e.TryGetValue("Total", out var t)) total = Convert.ToDouble(t);
        if (e.TryGetValue("Quantity", out var q)) qty = Convert.ToInt32(q);

        return new Invoice
        {
            Id = e.GetString("Id") ?? e.RowKey,
            InvoiceNo = e.GetString("InvoiceNo") ?? "",
            ClientId = e.GetString("ClientId") ?? "",
            BookingId = e.GetString("BookingId") ?? "",
            BookingLabel = e.GetString("BookingLabel") ?? "",
            IssueDate = e.GetString("IssueDate") ?? "",
            DueDate = e.GetString("DueDate") ?? "",
            Service = e.GetString("Service") ?? "",
            Quantity = qty,
            Price = price,
            Total = total,
            Status = e.GetString("Status") ?? InvoiceStatus.Unpaid,
            EmailSent = e.GetBoolean("EmailSent") ?? true,
            CreatedAt = new Timestamp(dt)
        };
    }
}

public class ServiceStore
{
    private readonly AzureTableContext _context;
    private const string TableName = "services";

    public ServiceStore(AzureTableContext context) => _context = context;

    public async Task<List<ServicePackage>> ListAsync()
    {
        var list = new List<ServicePackage>();
        var entities = await _context.QueryAllEntitiesAsync(TableName);
        foreach (var entity in entities)
        {
            list.Add(ToService(entity));
        }

        if (list.Count == 0)
        {
            foreach (var s in Defaults())
            {
                await SaveAsync(s);
            }
            return Defaults();
        }

        return list.OrderBy(s => s.SortOrder).ToList();
    }

    public async Task SaveAsync(ServicePackage s)
    {
        if (string.IsNullOrWhiteSpace(s.Name))
            throw new ArgumentException("Service package name is required.", nameof(s));

        if (string.IsNullOrWhiteSpace(s.Id)) s.Id = Guid.NewGuid().ToString("N")[..8];
        var entity = new TableEntity("service", s.Id)
        {
            { "Id", s.Id },
            { "Name", s.Name },
            { "Description", s.Description },
            { "BasePrice", s.BasePrice },
            { "IncludedHours", s.IncludedHours },
            { "ExtraHourPrice", s.ExtraHourPrice },
            { "HourlyOnly", s.HourlyOnly },
            { "SortOrder", s.SortOrder }
        };
        await _context.UpsertAsync(TableName, entity, TableUpdateMode.Replace);
    }

    private static ServicePackage ToService(TableEntity e)
    {
        double bp = 0, ehp = 0;
        int ih = 1, so = 0;
        bool ho = false;

        if (e.TryGetValue("BasePrice", out var v1)) bp = Convert.ToDouble(v1);
        if (e.TryGetValue("ExtraHourPrice", out var v2)) ehp = Convert.ToDouble(v2);
        if (e.TryGetValue("IncludedHours", out var v3)) ih = Convert.ToInt32(v3);
        if (e.TryGetValue("SortOrder", out var v4)) so = Convert.ToInt32(v4);
        if (e.TryGetValue("HourlyOnly", out var v5)) ho = Convert.ToBoolean(v5);

        return new ServicePackage
        {
            Id = e.GetString("Id") ?? e.RowKey,
            Name = e.GetString("Name") ?? "",
            Description = e.GetString("Description") ?? "",
            BasePrice = bp,
            IncludedHours = ih,
            ExtraHourPrice = ehp,
            HourlyOnly = ho,
            SortOrder = so
        };
    }

    public static List<ServicePackage> Defaults() => new()
    {
        new()
        {
            Id = "matric-ball",
            Name = "Matric Ball",
            Description = "2-3 hours of event & portrait coverage for Cape Flats & Cape Town schools.",
            BasePrice = 800,
            IncludedHours = 3,
            ExtraHourPrice = 0,
            HourlyOnly = false,
            SortOrder = 1
        },
        new()
        {
            Id = "wedding",
            Name = "Wedding",
            Description = "Starting from 4 hours (+R200 for each additional hour).",
            BasePrice = 1500,
            IncludedHours = 4,
            ExtraHourPrice = 200,
            HourlyOnly = false,
            SortOrder = 2
        },
        new()
        {
            Id = "corporate-event",
            Name = "Corporate Event",
            Description = "Starting from 4 hours (+R200 for each additional hour).",
            BasePrice = 2000,
            IncludedHours = 4,
            ExtraHourPrice = 200,
            HourlyOnly = false,
            SortOrder = 3
        },
        new()
        {
            Id = "other-events",
            Name = "Other Events / Birthday",
            Description = "Custom celebration coverage at R300 per hour (selectable hours).",
            BasePrice = 300,
            IncludedHours = 1,
            ExtraHourPrice = 300,
            HourlyOnly = true,
            SortOrder = 4
        },
        new()
        {
            Id = "ngo-community",
            Name = "Community / NGO & Youth Outreach",
            Description = "Subsidized non-profit coverage for registered charities, school milestones, and youth visual-literacy initiatives.",
            BasePrice = 450,
            IncludedHours = 3,
            ExtraHourPrice = 150,
            HourlyOnly = false,
            SortOrder = 5
        }
    };
}

public class ReviewStore
{
    private readonly AzureTableContext _context;
    private readonly AzureCloudSyncService? _cloudSync;
    private const string TableName = "reviews";

    public ReviewStore(AzureTableContext context, AzureCloudSyncService? cloudSync = null)
    {
        _context = context;
        _cloudSync = cloudSync;
    }

    public async Task CreateAsync(Review r)
    {
        if (string.IsNullOrWhiteSpace(r.Text))
            throw new ArgumentException("Review text is required.", nameof(r));

        if (string.IsNullOrWhiteSpace(r.Id)) r.Id = "rev-" + Guid.NewGuid().ToString("N")[..8];
        var entity = new TableEntity("review", r.Id)
        {
            { "Id", r.Id },
            { "ClientId", r.ClientId },
            { "ClientName", r.ClientName },
            { "authorName", r.ClientName },
            { "Rating", r.Rating },
            { "rating", r.Rating },
            { "Text", r.Text },
            { "text", r.Text },
            { "Approved", r.Approved },
            { "approved", r.Approved },
            { "CreatedAt", r.CreatedAt.ToString() }
        };
        await _context.UpsertAsync(TableName, entity, TableUpdateMode.Replace);
        if (_cloudSync != null && _cloudSync.IsEnabled)
        {
            await _cloudSync.PushReviewAsync(r);
        }
    }

    public async Task<List<Review>> ListByClientAsync(string clientId, string? clientName = null)
    {
        if (_cloudSync != null) await _cloudSync.EnsureSyncedAsync();
        var list = new List<Review>();
        var entities = await _context.QueryAllEntitiesAsync(TableName);
        foreach (var entity in entities)
        {
            var cid = entity.GetString("ClientId") ?? entity.GetString("clientId") ?? "";
            var cname = entity.GetString("ClientName") ?? entity.GetString("authorName") ?? "";
            if ((!string.IsNullOrWhiteSpace(clientId) && string.Equals(cid, clientId, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(clientName) && string.Equals(cname, clientName.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                list.Add(ToReview(entity));
            }
        }
        return list.OrderByDescending(r => r.CreatedAt.ToDateTime()).ToList();
    }

    public async Task<List<Review>> ListApprovedAsync()
    {
        if (_cloudSync != null) await _cloudSync.EnsureSyncedAsync();
        var list = new List<Review>();
        var entities = await _context.QueryAllEntitiesAsync(TableName);
        foreach (var entity in entities)
        {
            bool approved = entity.GetBoolean("Approved") ?? entity.GetBoolean("approved") ?? false;
            if (approved)
            {
                list.Add(ToReview(entity));
            }
        }
        return list.OrderByDescending(r => r.CreatedAt.ToDateTime()).ToList();
    }

    private static Review ToReview(TableEntity e)
    {
        var createdAtStr = e.GetString("CreatedAt") ?? e.GetString("createdAt");
        DateTime dt = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(createdAtStr)) DateTime.TryParse(createdAtStr, out dt);

        int rating = 5;
        if (e.TryGetValue("Rating", out var r1)) rating = Convert.ToInt32(r1);
        else if (e.TryGetValue("rating", out var r2)) rating = Convert.ToInt32(r2);

        bool approved = e.GetBoolean("Approved") ?? e.GetBoolean("approved") ?? false;

        return new Review
        {
            Id = e.GetString("Id") ?? e.GetString("id") ?? e.RowKey,
            ClientId = e.GetString("ClientId") ?? e.GetString("clientId") ?? "",
            ClientName = e.GetString("ClientName") ?? e.GetString("authorName") ?? "Anonymous",
            Rating = rating,
            Text = e.GetString("Text") ?? e.GetString("text") ?? "",
            Approved = approved,
            CreatedAt = new Timestamp(dt)
        };
    }
}

public class EnquiryStore
{
    private readonly AzureTableContext _context;
    private readonly AzureCloudSyncService? _cloudSync;
    private const string TableName = "enquiries";

    public EnquiryStore(AzureTableContext context, AzureCloudSyncService? cloudSync = null)
    {
        _context = context;
        _cloudSync = cloudSync;
    }

    public async Task CreateAsync(ContactEnquiry e)
    {
        if (string.IsNullOrWhiteSpace(e.FullName) || string.IsNullOrWhiteSpace(e.Email) || string.IsNullOrWhiteSpace(e.Message))
            throw new ArgumentException("Full name, email, and message are required for a contact enquiry.", nameof(e));

        if (string.IsNullOrWhiteSpace(e.Id)) e.Id = "enq-" + Guid.NewGuid().ToString("N")[..8];
        var entity = new TableEntity("enquiry", e.Id)
        {
            { "Id", e.Id },
            { "FullName", e.FullName },
            { "fullName", e.FullName },
            { "Email", e.Email },
            { "email", e.Email },
            { "Phone", e.Phone },
            { "phone", e.Phone },
            { "Message", e.Message },
            { "message", e.Message },
            { "Status", "pending" },
            { "status", "pending" },
            { "CreatedAt", e.CreatedAt.ToString() }
        };
        await _context.UpsertAsync(TableName, entity, TableUpdateMode.Replace);
        if (_cloudSync != null && _cloudSync.IsEnabled)
        {
            await _cloudSync.PushEnquiryAsync(e);
        }
    }

    public async Task<List<ContactEnquiry>> ListAllAsync()
    {
        if (_cloudSync != null) await _cloudSync.EnsureSyncedAsync();
        var list = new List<ContactEnquiry>();
        var entities = await _context.QueryAllEntitiesAsync(TableName);
        foreach (var entity in entities)
        {
            var createdAtStr = entity.GetString("CreatedAt") ?? entity.GetString("createdAt");
            DateTime dt = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(createdAtStr)) DateTime.TryParse(createdAtStr, out dt);

            list.Add(new ContactEnquiry
            {
                Id = entity.GetString("Id") ?? entity.RowKey,
                FullName = entity.GetString("FullName") ?? entity.GetString("fullName") ?? "",
                Email = entity.GetString("Email") ?? entity.GetString("email") ?? "",
                Phone = entity.GetString("Phone") ?? entity.GetString("phone") ?? "",
                Message = entity.GetString("Message") ?? entity.GetString("message") ?? "",
                CreatedAt = new Timestamp(dt)
            });
        }
        return list.OrderByDescending(e => e.CreatedAt.ToDateTime()).ToList();
    }
}

public class NotificationStore
{
    private readonly AzureTableContext _context;
    private const string TableName = "notifications";

    public NotificationStore(AzureTableContext context) => _context = context;

    public async Task<NotificationItem> CreateAsync(NotificationItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Message))
            throw new ArgumentException("Notification title and message are required.", nameof(item));

        if (string.IsNullOrWhiteSpace(item.Id))
            item.Id = "notif-" + Guid.NewGuid().ToString("N")[..8];

        var entity = new TableEntity("notification", item.Id)
        {
            { "Id", item.Id },
            { "ClientId", item.ClientId },
            { "RecipientEmail", item.RecipientEmail.Trim().ToLowerInvariant() },
            { "Title", item.Title },
            { "Message", item.Message },
            { "Category", item.Category },
            { "RelatedId", item.RelatedId },
            { "IsRead", item.IsRead },
            { "CreatedAt", item.CreatedAt.ToString() }
        };
        await _context.UpsertAsync(TableName, entity, TableUpdateMode.Replace);
        return item;
    }

    public async Task<List<NotificationItem>> ListByClientAsync(string clientId, string? email = null)
    {
        var list = new List<NotificationItem>();
        var entities = await _context.QueryAllEntitiesAsync(TableName);
        foreach (var entity in entities)
        {
            var cid = entity.GetString("ClientId") ?? "";
            var recEmail = entity.GetString("RecipientEmail") ?? "";
            if ((!string.IsNullOrWhiteSpace(clientId) && cid.Equals(clientId, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(email) && recEmail.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                list.Add(ToNotification(entity));
            }
        }
        return list.OrderByDescending(n => n.CreatedAt.ToDateTime()).ToList();
    }

    public async Task<List<NotificationItem>> ListAllAsync()
    {
        var entities = await _context.QueryAllEntitiesAsync(TableName);
        return entities.Select(ToNotification).OrderByDescending(n => n.CreatedAt.ToDateTime()).ToList();
    }

    public async Task MarkReadAsync(string id)
    {
        var entity = await _context.GetEntityOrNullAsync(TableName, "notification", id);
        if (entity != null)
        {
            entity["IsRead"] = true;
            await _context.UpsertAsync(TableName, entity, TableUpdateMode.Replace);
        }
    }

    private static NotificationItem ToNotification(TableEntity e)
    {
        var createdAtStr = e.GetString("CreatedAt");
        DateTime dt = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(createdAtStr)) DateTime.TryParse(createdAtStr, out dt);

        return new NotificationItem
        {
            Id = e.GetString("Id") ?? e.RowKey,
            ClientId = e.GetString("ClientId") ?? "",
            RecipientEmail = e.GetString("RecipientEmail") ?? "",
            Title = e.GetString("Title") ?? "",
            Message = e.GetString("Message") ?? "",
            Category = e.GetString("Category") ?? "Booking",
            RelatedId = e.GetString("RelatedId") ?? "",
            IsRead = e.GetBoolean("IsRead") ?? false,
            CreatedAt = new Timestamp(dt)
        };
    }
}
