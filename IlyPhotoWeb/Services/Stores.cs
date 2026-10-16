using System.Globalization;
using Azure;
using Azure.Data.Tables;
using IlyPhotoWeb.Models;

namespace IlyPhotoWeb.Services;

public class CounterStore
{
    private readonly TableClient _table;

    public CounterStore(AzureTableContext context) => _table = context.Counters;

    /// <summary>Atomically increments counters/{name} and returns the new value.</summary>
    public async Task<long> NextAsync(string name, long start)
    {
        try
        {
            var entity = await _table.GetEntityIfExistsAsync<TableEntity>("counter", name);
            long current = start;
            if (entity.HasValue && entity.Value != null && entity.Value.TryGetValue("Value", out var v))
            {
                current = v is long l ? l : Convert.ToInt64(v);
            }
            long next = current + 1;
            var newEntity = new TableEntity("counter", name) { { "Value", next } };
            await _table.UpsertEntityAsync(newEntity, TableUpdateMode.Replace);
            return next;
        }
        catch
        {
            return start + 1;
        }
    }
}

public class UserStore
{
    private readonly TableClient _table;

    public UserStore(AzureTableContext context) => _table = context.Users;

    public async Task<UserProfile?> GetAsync(string uid)
    {
        try
        {
            var entity = await _table.GetEntityIfExistsAsync<TableEntity>("user", uid);
            return entity.HasValue && entity.Value != null ? ToUserProfile(entity.Value) : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<UserProfile?> GetByEmailAsync(string email)
    {
        var cleanEmail = email.Trim().ToLowerInvariant();
        try
        {
            await foreach (var entity in _table.QueryAsync<TableEntity>())
            {
                var e = entity.GetString("Email") ?? entity.GetString("email") ?? "";
                if (e.Equals(cleanEmail, StringComparison.OrdinalIgnoreCase))
                {
                    return ToUserProfile(entity);
                }
            }
        }
        catch { }
        return null;
    }

    public async Task<UserProfile?> GetByClientIdAsync(string clientId)
    {
        try
        {
            await foreach (var entity in _table.QueryAsync<TableEntity>())
            {
                var cid = entity.GetString("ClientId") ?? entity.GetString("clientId") ?? "";
                if (cid.Equals(clientId, StringComparison.OrdinalIgnoreCase))
                {
                    return ToUserProfile(entity);
                }
            }
        }
        catch { }
        return null;
    }

    public async Task<List<UserProfile>> ListClientsAsync()
    {
        var list = new List<UserProfile>();
        try
        {
            await foreach (var entity in _table.QueryAsync<TableEntity>())
            {
                var role = entity.GetString("Role") ?? entity.GetString("role") ?? "";
                if (role.Equals(Roles.Client, StringComparison.OrdinalIgnoreCase) || role.Equals("client", StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(ToUserProfile(entity));
                }
            }
        }
        catch { }
        return list.OrderBy(u => u.ClientId, StringComparer.Ordinal).ToList();
    }

    public async Task SaveAsync(UserProfile p)
    {
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
        await _table.UpsertEntityAsync(entity, TableUpdateMode.Merge);
    }

    public async Task UpdateContactAsync(string uid, string fullName, string phone)
    {
        var user = await GetAsync(uid);
        if (user != null)
        {
            user.FullName = fullName;
            user.Phone = phone;
            await SaveAsync(user);
        }
    }

    public async Task UpdateRoleAsync(string uid, string role, string clientId)
    {
        var user = await GetAsync(uid);
        if (user != null)
        {
            user.Role = role;
            user.ClientId = clientId;
            await SaveAsync(user);
        }
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
    private readonly TableClient _table;
    private readonly TableClient _blockedTable;

    public BookingStore(AzureTableContext context)
    {
        _table = context.Bookings;
        _blockedTable = context.BlockedDates;
    }

    public async Task<Booking?> GetAsync(string id)
    {
        try
        {
            var entity = await _table.GetEntityIfExistsAsync<TableEntity>("booking", id);
            return entity.HasValue && entity.Value != null ? ToBooking(entity.Value) : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string> CreateAsync(Booking b)
    {
        if (string.IsNullOrWhiteSpace(b.Id)) b.Id = "book-" + Guid.NewGuid().ToString("N")[..8];
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
        await _table.UpsertEntityAsync(entity, TableUpdateMode.Replace);
        return b.Id;
    }

    public async Task<List<Booking>> ListAllAsync()
    {
        var list = new List<Booking>();
        try
        {
            await foreach (var entity in _table.QueryAsync<TableEntity>())
            {
                list.Add(ToBooking(entity));
            }
        }
        catch { }
        return list.OrderBy(b => b.Date).ThenBy(b => b.StartTime).ToList();
    }

    public async Task<List<Booking>> ListByClientAsync(string clientId)
    {
        var list = new List<Booking>();
        try
        {
            await foreach (var entity in _table.QueryAsync<TableEntity>())
            {
                var cid = entity.GetString("ClientId") ?? entity.GetString("clientId") ?? entity.GetString("Uid") ?? entity.GetString("uid");
                if (string.Equals(cid, clientId, StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(ToBooking(entity));
                }
            }
        }
        catch { }
        return list.OrderByDescending(b => b.Date).ToList();
    }

    public async Task<List<Booking>> ListByDateRangeAsync(string fromIso, string toIso)
    {
        var all = await ListAllAsync();
        return all.Where(b => string.Compare(b.Date, fromIso, StringComparison.Ordinal) >= 0 &&
                              string.Compare(b.Date, toIso, StringComparison.Ordinal) <= 0).ToList();
    }

    public async Task<bool> HasConfirmedOnDateAsync(string dateIso, string? exceptBookingId = null)
    {
        var all = await ListAllAsync();
        return all.Any(b => b.Date == dateIso && b.Status == BookingStatus.Confirmed && b.Id != exceptBookingId);
    }

    public async Task SetStatusAsync(string id, string status)
    {
        var b = await GetAsync(id);
        if (b != null)
        {
            b.Status = status;
            await CreateAsync(b);
        }
    }

    public async Task RescheduleAsync(string id, string date, string start, string end, int hours, double total)
    {
        var b = await GetAsync(id);
        if (b != null)
        {
            b.Date = date;
            b.StartTime = start;
            b.EndTime = end;
            b.Hours = hours;
            b.EstimatedTotal = total;
            await CreateAsync(b);
        }
    }

    // ---- blocked dates ----
    public async Task<bool> IsBlockedAsync(string dateIso)
    {
        try
        {
            var entity = await _blockedTable.GetEntityIfExistsAsync<TableEntity>("blockedDate", dateIso);
            return entity.HasValue;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<BlockedDate>> ListBlockedAsync(string fromIso, string toIso)
    {
        var list = new List<BlockedDate>();
        try
        {
            await foreach (var entity in _blockedTable.QueryAsync<TableEntity>())
            {
                var d = entity.GetString("Date") ?? entity.RowKey;
                if (string.Compare(d, fromIso, StringComparison.Ordinal) >= 0 &&
                    string.Compare(d, toIso, StringComparison.Ordinal) <= 0)
                {
                    list.Add(new BlockedDate
                    {
                        Id = entity.RowKey,
                        Date = d,
                        Reason = entity.GetString("Reason") ?? ""
                    });
                }
            }
        }
        catch { }
        return list.OrderBy(b => b.Date).ToList();
    }

    public async Task BlockAsync(string dateIso, string reason)
    {
        var entity = new TableEntity("blockedDate", dateIso)
        {
            { "Date", dateIso },
            { "Reason", reason },
            { "CreatedAt", DateTime.UtcNow.ToString("o") }
        };
        await _blockedTable.UpsertEntityAsync(entity, TableUpdateMode.Replace);
    }

    public async Task UnblockAsync(string dateIso)
    {
        try
        {
            await _blockedTable.DeleteEntityAsync("blockedDate", dateIso);
        }
        catch { }
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

        return new Booking
        {
            Id = e.GetString("Id") ?? e.GetString("id") ?? e.RowKey,
            ClientId = e.GetString("ClientId") ?? e.GetString("clientId") ?? "",
            Uid = e.GetString("Uid") ?? e.GetString("uid") ?? "",
            ClientName = e.GetString("ClientName") ?? e.GetString("clientName") ?? "",
            ClientEmail = e.GetString("ClientEmail") ?? e.GetString("clientEmail") ?? "",
            ClientPhone = e.GetString("ClientPhone") ?? e.GetString("clientPhone") ?? "",
            EventType = e.GetString("EventType") ?? e.GetString("serviceName") ?? "",
            Date = e.GetString("Date") ?? e.GetString("eventDate") ?? "",
            StartTime = e.GetString("StartTime") ?? e.GetString("eventTime") ?? "",
            EndTime = e.GetString("EndTime") ?? "",
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
    private readonly TableClient _table;
    private readonly CounterStore _counters;

    public InvoiceStore(AzureTableContext context, CounterStore counters)
    {
        _table = context.Invoices;
        _counters = counters;
    }

    public async Task<Invoice> CreateAsync(Invoice inv)
    {
        if (string.IsNullOrWhiteSpace(inv.Id)) inv.Id = "inv-" + Guid.NewGuid().ToString("N")[..8];
        var n = await _counters.NextAsync("invoices", 0);
        inv.InvoiceNo = $"INV-{n:D5}";
        inv.Total = Math.Round(inv.Quantity * inv.Price, 2);

        var entity = new TableEntity("invoice", inv.Id)
        {
            { "Id", inv.Id },
            { "InvoiceNo", inv.InvoiceNo },
            { "ClientId", inv.ClientId },
            { "BookingId", inv.BookingId },
            { "BookingLabel", inv.BookingLabel },
            { "IssueDate", inv.IssueDate },
            { "DueDate", inv.DueDate },
            { "Service", inv.Service },
            { "Quantity", inv.Quantity },
            { "Price", inv.Price },
            { "Total", inv.Total },
            { "Status", inv.Status },
            { "EmailSent", inv.EmailSent },
            { "CreatedAt", inv.CreatedAt.ToString() }
        };
        await _table.UpsertEntityAsync(entity, TableUpdateMode.Replace);
        return inv;
    }

    public async Task<List<Invoice>> ListAllAsync()
    {
        var list = new List<Invoice>();
        try
        {
            await foreach (var entity in _table.QueryAsync<TableEntity>())
            {
                list.Add(ToInvoice(entity));
            }
        }
        catch { }
        return list.OrderByDescending(i => i.InvoiceNo, StringComparer.Ordinal).ToList();
    }

    public async Task<List<Invoice>> ListByClientAsync(string clientId)
    {
        var list = new List<Invoice>();
        try
        {
            await foreach (var entity in _table.QueryAsync<TableEntity>())
            {
                var cid = entity.GetString("ClientId");
                if (string.Equals(cid, clientId, StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(ToInvoice(entity));
                }
            }
        }
        catch { }
        return list.OrderByDescending(i => i.InvoiceNo, StringComparer.Ordinal).ToList();
    }

    public async Task SetStatusAsync(string id, string status)
    {
        try
        {
            var entity = await _table.GetEntityIfExistsAsync<TableEntity>("invoice", id);
            if (entity.HasValue && entity.Value != null)
            {
                entity.Value["Status"] = status;
                await _table.UpsertEntityAsync(entity.Value, TableUpdateMode.Replace);
            }
        }
        catch { }
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
    private readonly TableClient _table;

    public ServiceStore(AzureTableContext context) => _table = context.Services;

    public async Task<List<ServicePackage>> ListAsync()
    {
        var list = new List<ServicePackage>();
        try
        {
            await foreach (var entity in _table.QueryAsync<TableEntity>())
            {
                list.Add(ToService(entity));
            }
        }
        catch { }

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
        await _table.UpsertEntityAsync(entity, TableUpdateMode.Replace);
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
            Description = "2-3 hours of event & portrait coverage.",
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
        }
    };
}

public class ReviewStore
{
    private readonly TableClient _table;

    public ReviewStore(AzureTableContext context) => _table = context.Reviews;

    public async Task CreateAsync(Review r)
    {
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
        await _table.UpsertEntityAsync(entity, TableUpdateMode.Replace);
    }

    public async Task<List<Review>> ListByClientAsync(string clientId)
    {
        var list = new List<Review>();
        try
        {
            await foreach (var entity in _table.QueryAsync<TableEntity>())
            {
                var cid = entity.GetString("ClientId") ?? entity.GetString("clientId");
                if (string.Equals(cid, clientId, StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(ToReview(entity));
                }
            }
        }
        catch { }
        return list.OrderByDescending(r => r.CreatedAt.ToDateTime()).ToList();
    }

    public async Task<List<Review>> ListApprovedAsync()
    {
        var list = new List<Review>();
        try
        {
            await foreach (var entity in _table.QueryAsync<TableEntity>())
            {
                bool approved = entity.GetBoolean("Approved") ?? entity.GetBoolean("approved") ?? false;
                if (approved)
                {
                    list.Add(ToReview(entity));
                }
            }
        }
        catch { }
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
    private readonly TableClient _table;

    public EnquiryStore(AzureTableContext context) => _table = context.Enquiries;

    public async Task CreateAsync(ContactEnquiry e)
    {
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
        await _table.UpsertEntityAsync(entity, TableUpdateMode.Replace);
    }

    public async Task<List<ContactEnquiry>> ListAllAsync()
    {
        var list = new List<ContactEnquiry>();
        try
        {
            await foreach (var entity in _table.QueryAsync<TableEntity>())
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
        }
        catch { }
        return list.OrderByDescending(e => e.CreatedAt.ToDateTime()).ToList();
    }
}
