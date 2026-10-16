using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace IlyPhotoWeb.Services;

/// <summary>
/// Photos are saved in a local folder (App_Data/photos) on this machine.
/// Signed expiring URLs are served safely through MediaController.
/// </summary>
public class LocalDiskPhotoStorage : IPhotoStorage
{
    private readonly string _root;
    private readonly byte[] _key;

    public LocalDiskPhotoStorage(IOptions<PhotoStorageOptions> options, IWebHostEnvironment env)
    {
        var o = options.Value;
        var configured = string.IsNullOrWhiteSpace(o.LocalPath) ? "App_Data/photos" : o.LocalPath;
        _root = Path.GetFullPath(Path.IsPathRooted(configured) ? configured : Path.Combine(env.ContentRootPath, configured));
        Directory.CreateDirectory(_root);

        _key = string.IsNullOrWhiteSpace(o.SigningKey) ? RandomNumberGenerator.GetBytes(32) : Encoding.UTF8.GetBytes(o.SigningKey);
    }

    private string PathFor(string objectName)
    {
        var full = Path.GetFullPath(Path.Combine(_root, objectName.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid photo path.");
        return full;
    }

    public async Task UploadAsync(string objectName, string contentType, Stream content, CancellationToken ct)
    {
        var path = PathFor(objectName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, ct);
    }

    public async Task DownloadToAsync(string objectName, Stream destination, CancellationToken ct)
    {
        await using var file = new FileStream(PathFor(objectName), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        await file.CopyToAsync(destination, ct);
    }

    public Task DeleteAsync(string objectName, CancellationToken ct)
    {
        var path = PathFor(objectName);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public string GetReadUrl(string objectName, TimeSpan lifetime)
    {
        var exp = DateTimeOffset.UtcNow.Add(lifetime).ToUnixTimeSeconds();
        var path = string.Join('/', objectName.Split('/').Select(Uri.EscapeDataString));
        return $"/media/{path}?exp={exp}&sig={Sign(objectName, exp)}";
    }

    public bool IsValid(string objectName, long exp, string? sig)
    {
        if (string.IsNullOrEmpty(sig) || DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp) return false;
        var expected = Encoding.ASCII.GetBytes(Sign(objectName, exp));
        var given = Encoding.ASCII.GetBytes(sig);
        return CryptographicOperations.FixedTimeEquals(expected, given);
    }

    public Stream? OpenRead(string objectName)
    {
        var path = PathFor(objectName);
        return File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
    }

    private string Sign(string objectName, long exp)
    {
        using var hmac = new HMACSHA256(_key);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{objectName}\n{exp}"));
        return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
