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

    private readonly TableClient _table;
    private readonly IPhotoStorage _photos;

    public GalleryStore(AzureTableContext context, IPhotoStorage photos)
    {
        _table = context.Galleries;
        _photos = photos;
    }

    public async Task<Gallery?> GetAsync(string id)
    {
        try
        {
            var entity = await _table.GetEntityIfExistsAsync<TableEntity>("gallery", id);
            return entity.HasValue && entity.Value != null ? ToGallery(entity.Value) : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<Gallery>> ListAllAsync()
    {
        var list = new List<Gallery>();
        try
        {
            await foreach (var entity in _table.QueryAsync<TableEntity>())
            {
                list.Add(ToGallery(entity));
            }
        }
        catch { }
        return Sort(list);
    }

    public async Task<List<Gallery>> ListByClientAsync(string clientId)
    {
        var all = await ListAllAsync();
        return all.Where(g => string.Equals(g.ClientId, clientId, StringComparison.OrdinalIgnoreCase)).ToList();
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
        await _table.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);

        return (gallery, skipped);
    }

    public async Task DeleteAsync(Gallery g)
    {
        foreach (var path in g.PhotoPaths)
        {
            await _photos.DeleteAsync(path, CancellationToken.None);
        }
        try
        {
            await _table.DeleteEntityAsync("gallery", g.Id);
        }
        catch { }
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
