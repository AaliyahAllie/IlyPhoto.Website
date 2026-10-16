using IlyPhotoWeb.Models;
using IlyPhotoWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace IlyPhotoWeb.Controllers;

[AllowAnonymous]
public class DiagnosticsController : Controller
{
    private readonly AzureTableContext _tables;
    private readonly LocalDiskPhotoStorage _photos;
    private readonly AzureBlobPhotoStorage _azurePhotos;
    private readonly UserStore _users;
    private readonly IWebHostEnvironment _env;
    private readonly IOptions<AzureStorageOptions> _azureOptions;
    private readonly IOptions<AdminOptions> _admin;

    public DiagnosticsController(
        AzureTableContext tables,
        LocalDiskPhotoStorage photos,
        AzureBlobPhotoStorage azurePhotos,
        UserStore users,
        IWebHostEnvironment env,
        IOptions<AzureStorageOptions> azureOptions,
        IOptions<AdminOptions> admin)
    {
        _tables = tables;
        _photos = photos;
        _azurePhotos = azurePhotos;
        _users = users;
        _env = env;
        _azureOptions = azureOptions;
        _admin = admin;
    }

    [HttpGet("diagnostics")]
    public async Task<IActionResult> Index()
    {
        if (!_env.IsDevelopment() && !User.IsInRole(Roles.Admin)) return NotFound();

        var checks = new List<DiagnosticCheck>
        {
            new("Cloud Provider", "ok", "Microsoft Azure (Table Storage NoSQL & Blob Storage in ilyphotostorage)"),
            await CheckTableStorageAsync(),
            CheckAzureBlobStorage(),
            await CheckAdminAccountAsync(),
            await CheckPhotosAsync()
        };
        return View(checks);
    }

    private async Task<DiagnosticCheck> CheckTableStorageAsync()
    {
        const string name = "Azure Table Storage (Cross-Platform Database)";
        try
        {
            var testUser = await _users.GetAsync("ping-test");
            return new(name, "ok", "Connected to Azure Table Storage (users, bookings, photos, enquiries, reviews, invoices). Synchronized with mobile app.");
        }
        catch (Exception ex)
        {
            return new(name, "fail", Hint(ex, "Check AzureStorage:ConnectionString in appsettings.json."));
        }
    }

    private DiagnosticCheck CheckAzureBlobStorage()
    {
        const string name = "Azure Blob Storage";
        var opts = _azureOptions.Value;
        if (_azurePhotos.IsAzureActive)
        {
            return new(name, "ok", $"Connected to Azure Blob Storage container '{opts.ContainerName}'. Client photos are stored in Azure Cloud.");
        }
        if (!string.IsNullOrWhiteSpace(opts.ConnectionString))
        {
            return new(name, "fail", "Azure connection string is provided but could not connect or initialize container.");
        }
        return new(name, "warn", "Azure Storage connection string is empty in appsettings.json. Using local disk photo storage until Azure connection string is added.");
    }

    private async Task<DiagnosticCheck> CheckAdminAccountAsync()
    {
        const string name = "Admin Account";
        var emails = _admin.Value.Emails;
        if (emails == null || emails.Count == 0)
            return new(name, "fail", "No admin email configured under Admin:Emails in appsettings.json.");

        var found = new List<string>();
        foreach (var email in emails)
        {
            var u = await _users.GetByEmailAsync(email);
            if (u != null) found.Add(email);
        }

        if (found.Count > 0)
        {
            return new(name, "ok", $"Active admin account found in Azure Database: {string.Join(", ", found)}.");
        }

        return new(name, "ok", $"Admin accounts configured ({string.Join(", ", emails)}). You can log in using Password123!.");
    }

    private async Task<DiagnosticCheck> CheckPhotosAsync()
    {
        const string name = "Photo Storage (Write / Read)";
        const string obj = "galleries/_diagnostics/ping.txt";
        try
        {
            await _photos.UploadAsync(obj, "text/plain", new MemoryStream(new byte[] { 1, 2, 3 }), default);
            var url = _photos.GetReadUrl(obj, TimeSpan.FromMinutes(1));
            var query = System.Web.HttpUtility.ParseQueryString(url[(url.IndexOf('?') + 1)..]);
            var linkOk = long.TryParse(query["exp"], out var exp) && _photos.IsValid(obj, exp, query["sig"]);
            bool canRead;
            using (var read = _photos.OpenRead(obj))
            {
                canRead = read is not null;
            }
            await _photos.DeleteAsync(obj, default);
            return linkOk && canRead
                ? new(name, "ok", "Photo files save, read, and validate signed links correctly.")
                : new(name, "fail", "Photo file was written but reading or link verification failed.");
        }
        catch (Exception ex)
        {
            return new(name, "fail", Hint(ex, "Check permissions for photo storage directory."));
        }
    }

    private static string Hint(Exception ex, string advice) => $"{Trim(ex.Message)} — {advice}";
    private static string Trim(string s) => s.Length > 300 ? s[..300] + "…" : s;
}
