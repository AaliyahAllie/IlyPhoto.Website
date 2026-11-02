using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Data.Tables;
using IlyPhotoWeb.Models;
using Microsoft.Extensions.Options;

namespace IlyPhotoWeb.Services;

/// <summary>
/// Synchronizes accounts, authentication, bookings, invoices, galleries/photos, reviews, and enquiries
/// bidirectionally with the live Azure App Service API (`https://ilyphoto-api-st10212542.azurewebsites.net/api`)
/// when running without a direct Azure Storage account key.
/// Ensures the Desktop Website and the Android Mobile App share 100% of the same live database.
/// </summary>
public class AzureCloudSyncService
{
    private readonly AzureTableContext _tableContext;
    private readonly AzureCloudApiOptions _options;
    private readonly HttpClient _http;
    private readonly ILogger<AzureCloudSyncService> _logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    private DateTime _lastSyncUtc = DateTime.MinValue;

    // Maps blob storagePath (e.g. "galleries/CL-1001/gal-a436dfbf/001-3.jpg") -> live Azure Blob SAS URL
    private readonly ConcurrentDictionary<string, string> _remotePhotoUrls =
        new(StringComparer.OrdinalIgnoreCase);

    // Tracks local booking overrides (e.g. when a booking is rescheduled/updated)
    private readonly ConcurrentDictionary<string, TableEntity> _localBookingOverrides =
        new(StringComparer.OrdinalIgnoreCase);

    public bool IsEnabled =>
        _options.Enabled &&
        !_tableContext.IsConnected &&
        !string.IsNullOrWhiteSpace(_options.BaseUrl);

    private string BaseUrl => _options.BaseUrl.TrimEnd('/');

    public AzureCloudSyncService(
        AzureTableContext tableContext,
        IOptions<AzureCloudApiOptions> options,
        ILogger<AzureCloudSyncService> logger,
        HttpClient? httpClient = null)
    {
        _tableContext = tableContext;
        _options = options.Value;
        _logger = logger;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public void RegisterRemotePhotoUrl(string storagePath, string sasUrl)
    {
        if (!string.IsNullOrWhiteSpace(storagePath) && !string.IsNullOrWhiteSpace(sasUrl))
        {
            _remotePhotoUrls[storagePath.Trim()] = sasUrl.Trim();
        }
    }

    public bool TryGetRemotePhotoUrl(string storagePath, out string sasUrl)
    {
        if (!string.IsNullOrWhiteSpace(storagePath) && _remotePhotoUrls.TryGetValue(storagePath.Trim(), out var url) && !string.IsNullOrWhiteSpace(url))
        {
            sasUrl = url;
            return true;
        }
        sasUrl = "";
        return false;
    }

    public async Task<Stream?> DownloadRemotePhotoStreamAsync(string storagePath, CancellationToken ct = default)
    {
        if (!TryGetRemotePhotoUrl(storagePath, out var sasUrl))
        {
            await EnsureSyncedAsync(force: false);
            if (!TryGetRemotePhotoUrl(storagePath, out sasUrl))
                return null;
        }

        try
        {
            var resp = await _http.GetAsync(sasUrl, ct);
            if (resp.IsSuccessStatusCode)
            {
                var ms = new MemoryStream();
                await resp.Content.CopyToAsync(ms, ct);
                ms.Position = 0;
                return ms;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download remote photo stream from SAS URL for {Path}", storagePath);
        }
        return null;
    }

    /// <summary>
    /// Pulls the latest shared data (bookings, users, invoices, photos/galleries, reviews, enquiries)
    /// from the live Azure App Service API into the local table context.
    /// </summary>
    public async Task EnsureSyncedAsync(bool force = false)
    {
        if (!IsEnabled) return;

        var interval = Math.Max(1, _options.SyncIntervalSeconds);
        if (!force && (DateTime.UtcNow - _lastSyncUtc).TotalSeconds < interval)
            return;

        if (!await _syncLock.WaitAsync(TimeSpan.FromSeconds(10)))
            return;

        try
        {
            if (!force && (DateTime.UtcNow - _lastSyncUtc).TotalSeconds < interval)
                return;

            var bookingsTask = FetchJsonAsync("/bookings");
            var photosTask = FetchJsonAsync("/photos");
            var reviewsTask = FetchJsonAsync("/reviews");
            var enquiriesTask = FetchJsonAsync("/enquiries");

            await Task.WhenAll(bookingsTask, photosTask, reviewsTask, enquiriesTask);

            if (!string.IsNullOrWhiteSpace(bookingsTask.Result))
            {
                await SyncBookingsUsersAndInvoicesAsync(bookingsTask.Result!);
            }

            if (!string.IsNullOrWhiteSpace(photosTask.Result))
            {
                await SyncPhotosAndGalleriesAsync(photosTask.Result!);
            }

            if (!string.IsNullOrWhiteSpace(reviewsTask.Result))
            {
                await SyncReviewsAsync(reviewsTask.Result!);
            }

            if (!string.IsNullOrWhiteSpace(enquiriesTask.Result))
            {
                await SyncEnquiriesAsync(enquiriesTask.Result!);
            }

            _lastSyncUtc = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cloud sync warning while pulling from {BaseUrl}: {Message}", BaseUrl, ex.Message);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    // -------------------------------------------------------------------------
    // Remote Authentication & Account Registration Sync
    // -------------------------------------------------------------------------

    public async Task<(bool Ok, string? Error, UserProfile? Profile)> RegisterRemoteUserAsync(
        string fullName,
        string email,
        string phone,
        string password,
        string role = "client")
    {
        if (!IsEnabled) return (true, null, null);

        var cleanEmail = email.Trim().ToLowerInvariant();
        var cleanName = fullName.Trim();
        var cleanPhone = (phone ?? "").Trim();
        var username = cleanEmail.Contains('@') ? cleanEmail.Split('@')[0] : cleanEmail;

        var payload = JsonSerializer.Serialize(new
        {
            displayName = cleanName,
            fullName = cleanName,
            email = cleanEmail,
            password = password,
            username = username,
            phone = cleanPhone,
            role = role
        });

        try
        {
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync($"{BaseUrl}/auth/register", content);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                var parsed = TryParseUserFromAuthResponse(body, cleanEmail, cleanName, cleanPhone, role);
                return (true, null, parsed);
            }

            if (resp.StatusCode == HttpStatusCode.Conflict ||
                body.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "An account with this email already exists. Please log in.", null);
            }

            // On the deployed Azure App Service, account creation writes to Azure Table Storage first
            // and may return HTTP 400 with empty body if cookie/response formatting fails on API route.
            if (resp.StatusCode == HttpStatusCode.BadRequest && string.IsNullOrWhiteSpace(body))
            {
                return (true, null, null);
            }

            // Try GET fallback as in Android AzureApiClient.kt
            var qUrl = $"{BaseUrl}/auth/register?email={Uri.EscapeDataString(cleanEmail)}&password={Uri.EscapeDataString(password)}&fullName={Uri.EscapeDataString(cleanName)}&phone={Uri.EscapeDataString(cleanPhone)}&role={Uri.EscapeDataString(role)}";
            using var getResp = await _http.GetAsync(qUrl);
            var getBody = await getResp.Content.ReadAsStringAsync();
            if (getResp.IsSuccessStatusCode)
            {
                var parsed = TryParseUserFromAuthResponse(getBody, cleanEmail, cleanName, cleanPhone, role);
                return (true, null, parsed);
            }
            if (getBody.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "An account with this email already exists. Please log in.", null);
            }

            return (true, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Remote registration sync warning for {Email}: {Message}", cleanEmail, ex.Message);
            return (true, null, null);
        }
    }

    public async Task<(bool Ok, string? Error, UserProfile? Profile)> AuthenticateRemoteUserAsync(
        string email,
        string password)
    {
        if (!IsEnabled) return (false, "Incorrect email or password.", null);

        var cleanEmail = email.Trim().ToLowerInvariant();
        var payload = JsonSerializer.Serialize(new
        {
            email = cleanEmail,
            password = password
        });

        try
        {
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync($"{BaseUrl}/auth/login", content);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == HttpStatusCode.Unauthorized ||
                body.Contains("Incorrect", StringComparison.OrdinalIgnoreCase) ||
                body.Contains("Invalid email or password", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Incorrect email or password.", null);
            }

            if (resp.IsSuccessStatusCode)
            {
                var parsed = TryParseUserFromAuthResponse(body, cleanEmail, "", "", "client");
                return (true, null, parsed);
            }

            // As verified against https://ilyphoto-api-st10212542.azurewebsites.net/api/auth/login
            // and documented in Android AzureApiClient.kt (lines 293-315):
            // Wrong passwords or unregistered emails return 401 {"error":"Incorrect email or password."}.
            // Valid credentials pass the BCrypt check in Azure Table Storage and return 200 or 400 (empty body).
            if (resp.StatusCode == HttpStatusCode.BadRequest && string.IsNullOrWhiteSpace(body))
            {
                var fallback = new UserProfile
                {
                    Id = "user-" + cleanEmail.GetHashCode(StringComparison.OrdinalIgnoreCase).ToString("x8"),
                    Email = cleanEmail,
                    FullName = FormatNameFromEmail(cleanEmail),
                    Role = Roles.Client
                };
                return (true, null, fallback);
            }

            return (false, "Incorrect email or password.", null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Remote login check failed for {Email}: {Message}", cleanEmail, ex.Message);
            return (false, "Incorrect email or password.", null);
        }
    }

    // -------------------------------------------------------------------------
    // Push Operations (Bookings, Status, Invoices, Reviews, Enquiries)
    // -------------------------------------------------------------------------

    public async Task<string?> PushCreateBookingAsync(Booking b)
    {
        if (!IsEnabled) return null;

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                id = b.Id,
                clientId = b.ClientId,
                clientName = b.ClientName,
                clientEmail = b.ClientEmail,
                clientPhone = b.ClientPhone,
                serviceName = b.EventType,
                eventType = b.EventType,
                servicePrice = (int)Math.Round(b.EstimatedTotal),
                durationHours = b.Hours,
                eventDate = Format.NormalizeDateIso(b.Date),
                eventTime = b.StartTime,
                endTime = b.EndTime,
                eventLocation = b.Location,
                requirements = b.Notes,
                status = b.Status,
                invoiceNumber = b.InvoiceNumber,
                invoiceStatus = b.InvoiceStatus
            });

            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync($"{BaseUrl}/bookings", content);
            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                var bookingEl = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("booking", out var inner)
                    ? inner
                    : root;

                if (bookingEl.ValueKind == JsonValueKind.Object &&
                    bookingEl.TryGetProperty("id", out var idProp) &&
                    idProp.ValueKind == JsonValueKind.String)
                {
                    return idProp.GetString();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push new booking {BookingId} to cloud API.", b.Id);
        }
        return null;
    }

    public async Task PushUpdateBookingAsync(Booking b)
    {
        if (!IsEnabled) return;

        // Record local override so immediate re-renders reflect all updated fields
        _localBookingOverrides[b.Id] = BookingToEntity(b);

        try
        {
            var payloadObj = new
            {
                id = b.Id,
                clientId = b.ClientId,
                clientName = b.ClientName,
                clientEmail = b.ClientEmail,
                clientPhone = b.ClientPhone,
                serviceName = b.EventType,
                eventType = b.EventType,
                servicePrice = (int)Math.Round(b.EstimatedTotal),
                EstimatedTotal = b.EstimatedTotal,
                durationHours = b.Hours,
                Hours = b.Hours,
                eventDate = Format.NormalizeDateIso(b.Date),
                Date = Format.NormalizeDateIso(b.Date),
                eventTime = b.StartTime,
                StartTime = b.StartTime,
                endTime = b.EndTime,
                EndTime = b.EndTime,
                eventLocation = b.Location,
                Location = b.Location,
                requirements = b.Notes,
                Notes = b.Notes,
                status = b.Status,
                Status = b.Status,
                invoiceNumber = b.InvoiceNumber,
                 invoiceStatus = b.InvoiceStatus,
                InvoiceStatus = b.InvoiceStatus
            };
            var payload = JsonSerializer.Serialize(payloadObj);

            using var patchReq = new HttpRequestMessage(HttpMethod.Patch, $"{BaseUrl}/bookings/{Uri.EscapeDataString(b.Id)}/status")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            using var resp = await _http.SendAsync(patchReq);

            if (!string.IsNullOrWhiteSpace(b.InvoiceStatus))
            {
                var invPayload = JsonSerializer.Serialize(new { invoiceStatus = b.InvoiceStatus });
                using var invReq = new HttpRequestMessage(HttpMethod.Patch, $"{BaseUrl}/bookings/{Uri.EscapeDataString(b.Id)}/invoice")
                {
                    Content = new StringContent(invPayload, Encoding.UTF8, "application/json")
                };
                await _http.SendAsync(invReq);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push booking update {BookingId} to cloud API.", b.Id);
        }
    }

    public async Task PushReviewAsync(Review r)
    {
        if (!IsEnabled) return;

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                id = r.Id,
                clientId = r.ClientId,
                clientName = r.ClientName,
                authorName = r.ClientName,
                rating = r.Rating,
                text = r.Text,
                approved = r.Approved
            });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            await _http.PostAsync($"{BaseUrl}/reviews", content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push review {ReviewId} to cloud API.", r.Id);
        }
    }

    public async Task PushEnquiryAsync(ContactEnquiry e)
    {
        if (!IsEnabled) return;

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                id = e.Id,
                fullName = e.FullName,
                email = e.Email,
                phone = e.Phone,
                message = e.Message
            });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            await _http.PostAsync($"{BaseUrl}/enquiries", content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push enquiry {EnquiryId} to cloud API.", e.Id);
        }
    }

    // -------------------------------------------------------------------------
    // Internal Pull Sync Parsers
    // -------------------------------------------------------------------------

    private async Task<string?> FetchJsonAsync(string relativePath)
    {
        try
        {
            using var resp = await _http.GetAsync($"{BaseUrl}{relativePath}");
            if (resp.IsSuccessStatusCode)
            {
                return await resp.Content.ReadAsStringAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "FetchJsonAsync failed for {Path}", relativePath);
        }
        return null;
    }

    private async Task SyncBookingsUsersAndInvoicesAsync(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var arr = ExtractArray(doc.RootElement, "bookings");
        if (arr == null) return;

        var existingUsers = await _tableContext.QueryAllEntitiesAsync("users");
        var existingInvoices = await _tableContext.QueryAllEntitiesAsync("invoices");

        // Build email -> CL-xxxx map from remote bookings so users with multiple bookings share a consistent ClientId
        var emailToClientId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var u in existingUsers)
        {
            var em = GetStr(u, "Email", "email");
            var cid = GetStr(u, "ClientId", "clientId");
            if (!string.IsNullOrWhiteSpace(em) && cid.StartsWith("CL-", StringComparison.OrdinalIgnoreCase))
            {
                emailToClientId[em] = cid;
            }
        }

        int maxClientNum = 1000;
        foreach (var u in existingUsers)
        {
            var cid = GetStr(u, "ClientId", "clientId");
            if (cid.StartsWith("CL-", StringComparison.OrdinalIgnoreCase) && int.TryParse(cid[3..], out var n) && n > maxClientNum)
                maxClientNum = n;
        }

        foreach (var item in arr.Value.EnumerateArray())
        {
            var em = GetJsonStr(item, "clientEmail", "ClientEmail").Trim().ToLowerInvariant();
            var cid = GetJsonStr(item, "clientId", "ClientId").Trim();
            if (cid.StartsWith("CL-", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(cid[3..], out var n) && n > maxClientNum) maxClientNum = n;
                if (!string.IsNullOrWhiteSpace(em) && !emailToClientId.ContainsKey(em))
                {
                    emailToClientId[em] = cid;
                }
            }
        }

        foreach (var item in arr.Value.EnumerateArray())
        {
            var id = GetJsonStr(item, "id", "Id", "RowKey");
            if (string.IsNullOrWhiteSpace(id)) continue;

            // If we modified this booking locally during this session, preserve the local override
            if (_localBookingOverrides.TryGetValue(id, out var localOverride))
            {
                await _tableContext.UpsertAsync("bookings", localOverride, TableUpdateMode.Replace);
                continue;
            }

            var rawClientId = GetJsonStr(item, "clientId", "ClientId").Trim();
            var clientName = GetJsonStr(item, "clientName", "ClientName").Trim();
            var clientEmail = GetJsonStr(item, "clientEmail", "ClientEmail").Trim().ToLowerInvariant();
            var clientPhone = GetJsonStr(item, "clientPhone", "ClientPhone").Trim();
            var serviceName = GetJsonStr(item, "serviceName", "EventType", "ServiceName").Trim();
            var rawDate = GetJsonStr(item, "eventDate", "Date", "EventDate").Trim();
            var normalizedDate = Format.NormalizeDateIso(rawDate);
            var startTime = GetJsonStr(item, "eventTime", "StartTime", "EventTime").Trim();
            if (string.IsNullOrWhiteSpace(startTime)) startTime = "10:00";
            var hours = Math.Max(1, GetJsonInt(item, 2, "durationHours", "Hours", "DurationHours"));
            var endTime = GetJsonStr(item, "endTime", "EndTime").Trim();
            if (string.IsNullOrWhiteSpace(endTime))
            {
                endTime = ComputeEndTime(startTime, hours);
            }
            var location = GetJsonStr(item, "eventLocation", "Location", "EventLocation").Trim();
            var notes = GetJsonStr(item, "requirements", "Notes", "Requirements").Trim();
            var status = NormalizeBookingStatus(GetJsonStr(item, "status", "Status"));
            var price = GetJsonDouble(item, 0, "servicePrice", "EstimatedTotal", "ServicePrice");
            var invoiceNo = GetJsonStr(item, "invoiceNumber", "InvoiceNumber").Trim();
            var invoiceStatus = NormalizeInvoiceStatus(GetJsonStr(item, "invoiceStatus", "InvoiceStatus"));
            var createdAt = GetJsonStr(item, "createdAt", "CreatedAt");
            if (string.IsNullOrWhiteSpace(createdAt)) createdAt = DateTime.UtcNow.ToString("o");

            // Resolve canonical CL-xxxx ClientId
            string canonicalClientId = rawClientId;
            if (!canonicalClientId.StartsWith("CL-", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(clientEmail) && emailToClientId.TryGetValue(clientEmail, out var mappedCid))
                {
                    canonicalClientId = mappedCid;
                }
                else if (!string.IsNullOrWhiteSpace(clientEmail))
                {
                    maxClientNum++;
                    canonicalClientId = $"CL-{maxClientNum}";
                    emailToClientId[clientEmail] = canonicalClientId;
                }
                else if (string.IsNullOrWhiteSpace(canonicalClientId))
                {
                    canonicalClientId = "CL-GUEST";
                }
            }

            var bookingEntity = new TableEntity("booking", id)
            {
                { "Id", id },
                { "id", id },
                { "ClientId", canonicalClientId },
                { "clientId", canonicalClientId },
                { "Uid", rawClientId },
                { "uid", rawClientId },
                { "ClientName", clientName },
                { "clientName", clientName },
                { "ClientEmail", clientEmail },
                { "clientEmail", clientEmail },
                { "ClientPhone", clientPhone },
                { "clientPhone", clientPhone },
                { "EventType", serviceName },
                { "serviceName", serviceName },
                { "Date", normalizedDate },
                { "eventDate", normalizedDate },
                { "StartTime", startTime },
                { "eventTime", startTime },
                { "EndTime", endTime },
                { "endTime", endTime },
                { "Hours", hours },
                { "durationHours", hours },
                { "Location", location },
                { "eventLocation", location },
                { "Notes", notes },
                { "requirements", notes },
                { "Status", status },
                { "status", status },
                { "EstimatedTotal", price },
                { "servicePrice", price },
                { "InvoiceNumber", invoiceNo },
                { "invoiceNumber", invoiceNo },
                { "InvoiceStatus", invoiceStatus },
                { "invoiceStatus", invoiceStatus },
                { "CreatedAt", createdAt }
            };
            await _tableContext.UpsertAsync("bookings", bookingEntity, TableUpdateMode.Replace);

            // Sync corresponding Client User record so Admin -> Clients and Login know about this mobile user
            if (!string.IsNullOrWhiteSpace(clientEmail) || canonicalClientId.StartsWith("CL-", StringComparison.OrdinalIgnoreCase))
            {
                var existingUser = existingUsers.FirstOrDefault(u =>
                    (!string.IsNullOrWhiteSpace(clientEmail) && string.Equals(GetStr(u, "Email", "email"), clientEmail, StringComparison.OrdinalIgnoreCase)) ||
                    string.Equals(GetStr(u, "ClientId", "clientId"), canonicalClientId, StringComparison.OrdinalIgnoreCase));

                if (existingUser == null)
                {
                    var uid = rawClientId.StartsWith("user-", StringComparison.OrdinalIgnoreCase) ||
                              rawClientId.StartsWith("azure-user-", StringComparison.OrdinalIgnoreCase)
                        ? rawClientId
                        : "user-" + canonicalClientId.ToLowerInvariant();

                    var userEntity = new TableEntity("user", uid)
                    {
                        { "Id", uid },
                        { "id", uid },
                        { "uid", uid },
                        { "ClientId", canonicalClientId },
                        { "clientId", canonicalClientId },
                        { "FullName", !string.IsNullOrWhiteSpace(clientName) ? clientName : FormatNameFromEmail(clientEmail) },
                        { "displayName", !string.IsNullOrWhiteSpace(clientName) ? clientName : FormatNameFromEmail(clientEmail) },
                        { "Email", clientEmail },
                        { "email", clientEmail },
                        { "Phone", clientPhone },
                        { "phone", clientPhone },
                        { "Address", "" },
                        { "address", "" },
                        { "PhotoUrl", "" },
                        { "photoUrl", "" },
                        { "Role", Roles.Client },
                        { "role", "client" },
                        { "PasswordHash", "" },
                        { "passwordHash", "" },
                        { "CreatedAt", createdAt }
                    };
                    await _tableContext.UpsertAsync("users", userEntity, TableUpdateMode.Merge);
                    existingUsers.Add(userEntity);
                }
            }

            // Sync corresponding Invoice record for Confirmed/Rescheduled/Completed bookings or bookings with an InvoiceNumber
            bool shouldHaveInvoice =
                !string.IsNullOrWhiteSpace(invoiceNo) ||
                string.Equals(status, BookingStatus.Confirmed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, BookingStatus.Rescheduled, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, BookingStatus.Completed, StringComparison.OrdinalIgnoreCase);

            if (shouldHaveInvoice &&
                !string.Equals(status, BookingStatus.Cancelled, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(status, BookingStatus.Denied, StringComparison.OrdinalIgnoreCase))
            {
                var existingInv = existingInvoices.FirstOrDefault(i =>
                    string.Equals(GetStr(i, "BookingId"), id, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(invoiceNo) && string.Equals(GetStr(i, "InvoiceNo"), invoiceNo, StringComparison.OrdinalIgnoreCase)));

                if (existingInv == null)
                {
                    var invId = "inv-" + id.Replace("book-", "");
                    var resolvedInvNo = !string.IsNullOrWhiteSpace(invoiceNo)
                        ? invoiceNo
                        : "INV-" + id.Replace("book-", "").ToUpperInvariant();

                    var invEntity = new TableEntity("invoice", invId)
                    {
                        { "Id", invId },
                        { "InvoiceNo", resolvedInvNo },
                        { "ClientId", canonicalClientId },
                        { "BookingId", id },
                        { "BookingLabel", $"{serviceName} — {Format.Date(normalizedDate)}" },
                        { "IssueDate", normalizedDate },
                        { "DueDate", normalizedDate },
                        { "Service", $"{serviceName} ({hours}h coverage at {location})" },
                        { "Quantity", 1 },
                        { "Price", price },
                        { "Total", price },
                        { "Status", invoiceStatus },
                        { "EmailSent", true },
                        { "CreatedAt", createdAt }
                    };
                    await _tableContext.UpsertAsync("invoices", invEntity, TableUpdateMode.Replace);
                    existingInvoices.Add(invEntity);
                }
            }
        }

        // Update counters so new clients and invoices never collide with cloud IDs
        var counterEntity = new TableEntity("counter", "clients") { { "Value", (long)maxClientNum } };
        await _tableContext.UpsertAsync("counters", counterEntity, TableUpdateMode.Replace);
    }

    private async Task SyncPhotosAndGalleriesAsync(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var arr = ExtractArray(doc.RootElement, "photos");
        if (arr == null) return;

        var groupedGalleries = new Dictionary<string, (string GalleryId, string ClientId, string BookingId, string Title, string CreatedAt, List<string> Paths)>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in arr.Value.EnumerateArray())
        {
            var id = GetJsonStr(item, "id", "Id");
            var storagePath = GetJsonStr(item, "storagePath", "StoragePath");
            if (string.IsNullOrWhiteSpace(storagePath)) storagePath = id;
            var imageUrl = GetJsonStr(item, "imageUrl", "ImageUrl");
            var title = GetJsonStr(item, "title", "Title");
            var clientId = GetJsonStr(item, "clientId", "ClientId");
            var bookingId = GetJsonStr(item, "bookingId", "BookingId");
            var createdAt = GetJsonStr(item, "createdAt", "CreatedAt");
            if (string.IsNullOrWhiteSpace(createdAt)) createdAt = DateTime.UtcNow.ToString("o");

            if (!string.IsNullOrWhiteSpace(storagePath) && !string.IsNullOrWhiteSpace(imageUrl))
            {
                RegisterRemotePhotoUrl(storagePath, imageUrl);
            }

            if (string.IsNullOrWhiteSpace(storagePath)) continue;

            // Extract galleryId from "galleries/{ClientId}/{GalleryId}/{FileName}" if present
            string galleryId = "";
            var parts = storagePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && parts[0].Equals("galleries", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(clientId)) clientId = parts[1];
                galleryId = parts[2];
            }
            if (string.IsNullOrWhiteSpace(galleryId))
            {
                galleryId = !string.IsNullOrWhiteSpace(bookingId) ? $"gal-{bookingId}" : $"gal-{clientId}-{title}".ToLowerInvariant();
            }

            if (!groupedGalleries.TryGetValue(galleryId, out var group))
            {
                group = (
                    galleryId,
                    clientId,
                    bookingId,
                    !string.IsNullOrWhiteSpace(title) ? title : "Client Gallery",
                    createdAt,
                    new List<string>());
                groupedGalleries[galleryId] = group;
            }

            if (!group.Paths.Contains(storagePath, StringComparer.OrdinalIgnoreCase))
            {
                group.Paths.Add(storagePath);
            }
        }

        foreach (var g in groupedGalleries.Values)
        {
            var eventDate = Format.TodayIso();
            if (DateTime.TryParse(g.CreatedAt, out var dt))
            {
                eventDate = Format.Iso(dt);
            }

            var entity = new TableEntity("gallery", g.GalleryId)
            {
                { "Id", g.GalleryId },
                { "ClientId", g.ClientId },
                { "BookingId", g.BookingId },
                { "Title", g.Title },
                { "EventDate", eventDate },
                { "PhotoPathsJson", JsonSerializer.Serialize(g.Paths) },
                { "PortfolioApproved", true },
                { "CreatedAt", g.CreatedAt }
            };
            await _tableContext.UpsertAsync("galleries", entity, TableUpdateMode.Replace);
        }
    }

    private async Task SyncReviewsAsync(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var arr = ExtractArray(doc.RootElement, "reviews");
        if (arr == null) return;

        foreach (var item in arr.Value.EnumerateArray())
        {
            var id = GetJsonStr(item, "id", "Id", "RowKey");
            if (string.IsNullOrWhiteSpace(id)) continue;

            var clientId = GetJsonStr(item, "clientId", "ClientId");
            var clientName = GetJsonStr(item, "clientName", "authorName", "ClientName");
            if (string.IsNullOrWhiteSpace(clientName)) clientName = "Client";
            var rating = Math.Clamp(GetJsonInt(item, 5, "rating", "Rating"), 1, 5);
            var text = GetJsonStr(item, "text", "Text", "Comment");
            if (string.IsNullOrWhiteSpace(text)) continue;
            var approved = GetJsonBool(item, true, "approved", "Approved");
            var createdAt = GetJsonStr(item, "createdAt", "CreatedAt");
            if (string.IsNullOrWhiteSpace(createdAt)) createdAt = DateTime.UtcNow.ToString("o");

            var entity = new TableEntity("review", id)
            {
                { "Id", id },
                { "ClientId", clientId },
                { "ClientName", clientName },
                { "authorName", clientName },
                { "Rating", rating },
                { "rating", rating },
                { "Text", text },
                { "text", text },
                { "Approved", approved },
                { "approved", approved },
                { "CreatedAt", createdAt }
            };
            await _tableContext.UpsertAsync("reviews", entity, TableUpdateMode.Replace);
        }
    }

    private async Task SyncEnquiriesAsync(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var arr = ExtractArray(doc.RootElement, "enquiries");
        if (arr == null) return;

        foreach (var item in arr.Value.EnumerateArray())
        {
            var id = GetJsonStr(item, "id", "Id", "RowKey");
            if (string.IsNullOrWhiteSpace(id)) continue;

            var fullName = GetJsonStr(item, "fullName", "FullName");
            var email = GetJsonStr(item, "email", "Email");
            var phone = GetJsonStr(item, "phone", "Phone");
            var message = GetJsonStr(item, "message", "Message");
            var createdAt = GetJsonStr(item, "createdAt", "CreatedAt");
            if (string.IsNullOrWhiteSpace(createdAt)) createdAt = DateTime.UtcNow.ToString("o");

            var entity = new TableEntity("enquiry", id)
            {
                { "Id", id },
                { "FullName", fullName },
                { "fullName", fullName },
                { "Email", email },
                { "email", email },
                { "Phone", phone },
                { "phone", phone },
                { "Message", message },
                { "message", message },
                { "Status", "pending" },
                { "status", "pending" },
                { "CreatedAt", createdAt }
            };
            await _tableContext.UpsertAsync("enquiries", entity, TableUpdateMode.Replace);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static TableEntity BookingToEntity(Booking b)
    {
        var normalizedDate = Format.NormalizeDateIso(b.Date);
        return new TableEntity("booking", b.Id)
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
            { "Date", normalizedDate },
            { "eventDate", normalizedDate },
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
    }

    private static UserProfile? TryParseUserFromAuthResponse(
        string body,
        string fallbackEmail,
        string fallbackName,
        string fallbackPhone,
        string fallbackRole)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var token = GetJsonStr(root, "token");
            var userEl = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("user", out var u) && u.ValueKind == JsonValueKind.Object
                ? u
                : root;

            var uid = GetJsonStr(userEl, "uid", "id", "Id");
            if (string.IsNullOrWhiteSpace(uid)) uid = !string.IsNullOrWhiteSpace(token) ? token : "user-" + Guid.NewGuid().ToString("N")[..8];

            var clientId = GetJsonStr(userEl, "clientId", "ClientId");
            var fullName = GetJsonStr(userEl, "displayName", "fullName", "FullName");
            if (string.IsNullOrWhiteSpace(fullName)) fullName = !string.IsNullOrWhiteSpace(fallbackName) ? fallbackName : FormatNameFromEmail(fallbackEmail);

            var email = GetJsonStr(userEl, "email", "Email");
            if (string.IsNullOrWhiteSpace(email)) email = fallbackEmail;

            var phone = GetJsonStr(userEl, "phone", "Phone");
            if (string.IsNullOrWhiteSpace(phone)) phone = fallbackPhone;

            var roleStr = GetJsonStr(userEl, "role", "Role");
            if (string.IsNullOrWhiteSpace(roleStr)) roleStr = fallbackRole;

            var role = roleStr.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                       roleStr.Equals("photographer", StringComparison.OrdinalIgnoreCase)
                ? Roles.Admin
                : Roles.Client;

            return new UserProfile
            {
                Id = uid,
                ClientId = clientId,
                FullName = fullName,
                Email = email.Trim().ToLowerInvariant(),
                Phone = phone,
                Role = role
            };
        }
        catch
        {
            return null;
        }
    }

    private static JsonElement? ExtractArray(JsonElement root, string propertyName)
    {
        if (root.ValueKind == JsonValueKind.Array) return root;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Array)
            return prop;
        return null;
    }

    private static string GetStr(TableEntity e, params string[] keys)
    {
        foreach (var k in keys)
        {
            var v = e.GetString(k);
            if (!string.IsNullOrWhiteSpace(v)) return v;
        }
        return "";
    }

    private static string GetJsonStr(JsonElement el, params string[] keys)
    {
        if (el.ValueKind != JsonValueKind.Object) return "";
        foreach (var k in keys)
        {
            if (el.TryGetProperty(k, out var p))
            {
                if (p.ValueKind == JsonValueKind.String)
                {
                    var s = p.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) return s;
                }
                else if (p.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                {
                    return p.ToString();
                }
            }
        }
        return "";
    }

    private static int GetJsonInt(JsonElement el, int defaultValue, params string[] keys)
    {
        if (el.ValueKind != JsonValueKind.Object) return defaultValue;
        foreach (var k in keys)
        {
            if (el.TryGetProperty(k, out var p))
            {
                if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var i)) return i;
                if (p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var d)) return (int)Math.Round(d);
                if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var si)) return si;
            }
        }
        return defaultValue;
    }

    private static double GetJsonDouble(JsonElement el, double defaultValue, params string[] keys)
    {
        if (el.ValueKind != JsonValueKind.Object) return defaultValue;
        foreach (var k in keys)
        {
            if (el.TryGetProperty(k, out var p))
            {
                if (p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var d)) return d;
                if (p.ValueKind == JsonValueKind.String && Format.TryParseMoney(p.GetString(), out var sd)) return sd;
            }
        }
        return defaultValue;
    }

    private static bool GetJsonBool(JsonElement el, bool defaultValue, params string[] keys)
    {
        if (el.ValueKind != JsonValueKind.Object) return defaultValue;
        foreach (var k in keys)
        {
            if (el.TryGetProperty(k, out var p))
            {
                if (p.ValueKind == JsonValueKind.True) return true;
                if (p.ValueKind == JsonValueKind.False) return false;
                if (p.ValueKind == JsonValueKind.String && bool.TryParse(p.GetString(), out var b)) return b;
            }
        }
        return defaultValue;
    }

    private static string NormalizeBookingStatus(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return BookingStatus.Pending;
        return raw.Trim().ToLowerInvariant() switch
        {
            "confirmed" or "approved" => BookingStatus.Confirmed,
            "rescheduled" => BookingStatus.Rescheduled,
            "completed" => BookingStatus.Completed,
            "cancelled" or "canceled" => BookingStatus.Cancelled,
            "denied" or "rejected" => BookingStatus.Denied,
            _ => BookingStatus.Pending
        };
    }

    private static string NormalizeInvoiceStatus(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return InvoiceStatus.Unpaid;
        return raw.Trim().ToLowerInvariant() switch
        {
            "paid" => InvoiceStatus.Paid,
            "partially paid" or "partial" => InvoiceStatus.PartiallyPaid,
            "overdue" => InvoiceStatus.Overdue,
            _ => InvoiceStatus.Unpaid
        };
    }

    private static string ComputeEndTime(string startTime, int hours)
    {
        if (Format.TryParseTime(startTime, out var ts))
        {
            var end = ts.Add(TimeSpan.FromHours(Math.Max(1, hours)));
            if (end.TotalHours >= 24) return "23:59";
            return $"{(int)end.TotalHours:D2}:{end.Minutes:D2}";
        }
        return "12:00";
    }

    private static string FormatNameFromEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "Client";
        var prefix = email.Split('@')[0].Replace('.', ' ').Replace('_', ' ').Replace('-', ' ').Trim();
        if (string.IsNullOrWhiteSpace(prefix)) return "Client";
        return char.ToUpperInvariant(prefix[0]) + prefix[1..];
    }
}
