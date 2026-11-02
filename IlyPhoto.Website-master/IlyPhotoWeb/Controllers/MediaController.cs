using IlyPhotoWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace IlyPhotoWeb.Controllers;

[AllowAnonymous]
public class MediaController : Controller
{
    private static readonly FileExtensionContentTypeProvider Types = new();
    private readonly LocalDiskPhotoStorage _localStorage;
    private readonly AzureBlobPhotoStorage _azureStorage;

    public MediaController(LocalDiskPhotoStorage localStorage, AzureBlobPhotoStorage azureStorage)
    {
        _localStorage = localStorage;
        _azureStorage = azureStorage;
    }

    [HttpGet("media/{**path}")]
    public async Task<IActionResult> Get(string path, long exp, string? sig)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith("galleries/"))
            return NotFound();

        // Validate the signed expiring link token
        if (!_localStorage.IsValid(path, exp, sig))
            return NotFound();

        var stream = await _azureStorage.OpenReadAsync(path);
        if (stream is null)
            return NotFound();

        if (!Types.TryGetContentType(path, out var contentType)) contentType = "application/octet-stream";
        Response.Headers.CacheControl = "private, max-age=600";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(stream, contentType);
    }
}
