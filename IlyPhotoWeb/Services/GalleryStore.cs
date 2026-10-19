using System.IO.Compression;
using System.Text.Json;
using Azure.Data.Tables;
using IlyPhotoWeb.Models;

namespace IlyPhotoWeb.Services;

/// <summary>Gallery metadata lives in Azure Table Storage; photo files live in Azure Blob Storage.</summary>
public class GalleryStore
{
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
    public const long MaxPhotoBytes = 50L * 1024 * 1024;   // 50 MB per photo

    private readonly AzureTableContext _context;
    private readonly IPhotoStorage _photos;
    private readonly AzureCloudSyncService? _cloudSync;
    private const string TableName = "galleries";

    public GalleryStore(AzureTableContext context, IPhotoStorage photos, AzureCloudSyncService? cloudSync = null)
    {
        _context = context;
        _photos = photos;
        _cloudSync = cloudSync;
    }

    public async Task<Gallery?> GetAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (_cloudSync != null) await _cloudSync.EnsureSyncedAsync();
        var entity = await _context.GetEntityOrNullAsync(TableName, "gallery", id);
        return entity != null ? ToGallery(entity) : null;
    }

    public async Task<List<Gallery>> ListAllAsync()
    {
        if (_cloudSync != null) await _cloudSync.EnsureSyncedAsync();
        var entities = await _context.QueryAllEntitiesAsync(TableName);
        var list = entities.Select(ToGallery).ToList();
        return Sort(list);
    }

    public async Task<List<Gallery>> ListByClientAsync(string clientId, string? email = null, string? uid = null)
    {
        var all = await ListAllAsync();
        return all.Where(g =>
            (!string.IsNullOrWhiteSpace(clientId) && string.Equals(g.ClientId, clientId, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(email) && (string.Equals(g.ClientId, email.Trim(), StringComparison.OrdinalIgnoreCase) || g.Title.Contains(email.Trim(), StringComparison.OrdinalIgnoreCase))) ||
            (!string.IsNullOrWhiteSpace(uid) && string.Equals(g.ClientId, uid, StringComparison.OrdinalIgnoreCase))
        ).ToList();
    }

    public async Task<List<Gallery>> ListPortfolioAsync()
    {
        var all = await ListAllAsync();
        return all.Where(g => g.PortfolioApproved).ToList();
    }

    /// <summary>
    /// Uploads files under galleries/{clientId}/{galleryId}/ and saves the gallery document in Azure Table Storage.
    /// Returns (gallery, number of files skipped).
    /// </summary>
    public async Task<(Gallery Gallery, int Skipped)> CreateWithPhotosAsync(
        Gallery gallery, IEnumerable<IFormFile> files, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(gallery.Id))
        {
            gallery.Id = "gal-" + Guid.NewGuid().ToString("N")[..8];
        }
        gallery.CreatedAt = Timestamp.GetCurrentTimestamp();

        int skipped = 0, index = 0;
        foreach (var file in files)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (file.Length == 0 || file.Length > MaxPhotoBytes || !AllowedExtensions.Contains(ext)
                || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                skipped++;
                continue;
            }
            index++;
            var objectName = $"galleries/{gallery.ClientId}/{gallery.Id}/{index:D3}-{Format.SafeFileName(file.FileName)}";
            await using var stream = file.OpenReadStream();
            await _photos.UploadAsync(objectName, file.ContentType, stream, ct);
            gallery.PhotoPaths.Add(objectName);
        }

        var entity = new TableEntity("gallery", gallery.Id)
        {
            { "Id", gallery.Id },
            { "ClientId", gallery.ClientId },
            { "BookingId", gallery.BookingId },
            { "Title", gallery.Title },
            { "EventDate", gallery.EventDate },
            { "PhotoPathsJson", JsonSerializer.Serialize(gallery.PhotoPaths) },
            { "PortfolioApproved", gallery.PortfolioApproved },
            { "CreatedAt", gallery.CreatedAt.ToString() }
        };
        await _context.UpsertAsync(TableName, entity, TableUpdateMode.Replace, ct);

        return (gallery, skipped);
    }

    public async Task DeleteAsync(Gallery g)
    {
        foreach (var path in g.PhotoPaths)
        {
            await _photos.DeleteAsync(path, CancellationToken.None);
        }
        await _context.DeleteAsync(TableName, "gallery", g.Id);
    }

    /// <summary>Short-lived signed private link to one photo.</summary>
    public string SignedUrl(string objectName, int minutes = 60) =>
        _photos.GetReadUrl(objectName, TimeSpan.FromMinutes(minutes));

    /// <summary>Streams every photo in the album into a zip written straight to the output stream.</summary>
    public async Task WriteZipAsync(Gallery g, Stream output, CancellationToken ct)
    {
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        foreach (var path in g.PhotoPaths)
        {
            var entry = zip.CreateEntry(Path.GetFileName(path), CompressionLevel.NoCompression);
            await using var entryStream = entry.Open();
            await _photos.DownloadToAsync(path, entryStream, ct);
        }
    }

    private static Gallery ToGallery(TableEntity e)
    {
        var createdAtStr = e.GetString("CreatedAt");
        DateTime dt = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(createdAtStr)) DateTime.TryParse(createdAtStr, out dt);

        var paths = new List<string>();
        var json = e.GetString("PhotoPathsJson");
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                paths = JsonSerializer.Deserialize<List<string>>(json) ?? new();
            }
            catch { }
        }

        return new Gallery
        {
            Id = e.GetString("Id") ?? e.RowKey,
            ClientId = e.GetString("ClientId") ?? "",
            BookingId = e.GetString("BookingId") ?? "",
            Title = e.GetString("Title") ?? "",
            EventDate = e.GetString("EventDate") ?? "",
            PhotoPaths = paths,
            PortfolioApproved = e.GetBoolean("PortfolioApproved") ?? false,
            CreatedAt = new Timestamp(dt)
        };
    }

    private static List<Gallery> Sort(IEnumerable<Gallery> list) =>
        list.OrderByDescending(g => g.EventDate, StringComparer.Ordinal).ToList();
}
