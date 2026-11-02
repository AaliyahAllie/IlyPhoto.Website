using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;

namespace IlyPhotoWeb.Services;

/// <summary>
/// Cloud photo storage using Azure Blob Storage.
/// Uploads photos directly to an Azure Storage Account container so customers can access images from anywhere.
/// Supports both direct Azure SAS URLs and local fallback.
/// </summary>
public class AzureBlobPhotoStorage : IPhotoStorage
{
    private readonly BlobContainerClient? _containerClient;
    private readonly LocalDiskPhotoStorage _localFallback;
    private readonly AzureCloudSyncService? _cloudSync;
    private readonly ILogger<AzureBlobPhotoStorage> _logger;
    private readonly bool _isConfigured;

    public AzureBlobPhotoStorage(
        IOptions<AzureStorageOptions> azureOptions,
        LocalDiskPhotoStorage localFallback,
        ILogger<AzureBlobPhotoStorage> logger,
        AzureCloudSyncService? cloudSync = null)
    {
        _localFallback = localFallback;
        _logger = logger;
        _cloudSync = cloudSync;
        var opts = azureOptions.Value;

        if (!string.IsNullOrWhiteSpace(opts.ConnectionString))
        {
            try
            {
                var serviceClient = new BlobServiceClient(opts.ConnectionString);
                var containerName = string.IsNullOrWhiteSpace(opts.ContainerName) ? "ilyphoto-galleries" : opts.ContainerName.ToLowerInvariant();
                _containerClient = serviceClient.GetBlobContainerClient(containerName);
                _containerClient.CreateIfNotExists(PublicAccessType.None);
                _isConfigured = true;
                _logger.LogInformation("Connected to Azure Blob Storage container: {ContainerName}", containerName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Azure Blob Storage. Falling back to cloud sync / local disk storage.");
                _isConfigured = false;
            }
        }
        else
        {
            _isConfigured = false;
        }
    }

    public bool IsAzureActive => _isConfigured && _containerClient != null;

    public async Task UploadAsync(string objectName, string contentType, Stream content, CancellationToken ct)
    {
        if (IsAzureActive)
        {
            try
            {
                var blobClient = _containerClient!.GetBlobClient(objectName);
                var uploadOptions = new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
                };
                await blobClient.UploadAsync(content, uploadOptions, ct);
                _logger.LogInformation("Uploaded image to Azure Blob Storage: {ObjectName}", objectName);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload to Azure Blob Storage for {ObjectName}. Trying local fallback.", objectName);
            }
        }

        // Fallback to local storage if Azure is not configured or throws
        if (content.CanSeek) content.Position = 0;
        await _localFallback.UploadAsync(objectName, contentType, content, ct);
    }

    public async Task DownloadToAsync(string objectName, Stream destination, CancellationToken ct)
    {
        if (IsAzureActive)
        {
            try
            {
                var blobClient = _containerClient!.GetBlobClient(objectName);
                if (await blobClient.ExistsAsync(ct))
                {
                    await blobClient.DownloadToAsync(destination, ct);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not download from Azure Blob Storage: {ObjectName}", objectName);
            }
        }

        if (_cloudSync != null && _cloudSync.IsEnabled)
        {
            await using var remoteStream = await _cloudSync.DownloadRemotePhotoStreamAsync(objectName, ct);
            if (remoteStream != null)
            {
                await remoteStream.CopyToAsync(destination, ct);
                return;
            }
        }

        await _localFallback.DownloadToAsync(objectName, destination, ct);
    }

    public async Task DeleteAsync(string objectName, CancellationToken ct)
    {
        if (IsAzureActive)
        {
            try
            {
                var blobClient = _containerClient!.GetBlobClient(objectName);
                await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete blob from Azure: {ObjectName}", objectName);
            }
        }

        await _localFallback.DeleteAsync(objectName, ct);
    }

    public string GetReadUrl(string objectName, TimeSpan lifetime)
    {
        if (IsAzureActive)
        {
            try
            {
                var blobClient = _containerClient!.GetBlobClient(objectName);
                if (blobClient.CanGenerateSasUri)
                {
                    var sasBuilder = new BlobSasBuilder
                    {
                        BlobContainerName = _containerClient.Name,
                        BlobName = objectName,
                        Resource = "b",
                        ExpiresOn = DateTimeOffset.UtcNow.Add(lifetime)
                    };
                    sasBuilder.SetPermissions(BlobSasPermissions.Read);
                    return blobClient.GenerateSasUri(sasBuilder).ToString();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not generate Azure SAS URL for {ObjectName}. Using media proxy.", objectName);
            }
        }

        if (_cloudSync != null && _cloudSync.TryGetRemotePhotoUrl(objectName, out var remoteSasUrl))
        {
            return remoteSasUrl;
        }

        // Proxy through MediaController with signed HMAC token
        return _localFallback.GetReadUrl(objectName, lifetime);
    }

    public async Task<Stream?> OpenReadAsync(string objectName, CancellationToken ct = default)
    {
        if (IsAzureActive)
        {
            try
            {
                var blobClient = _containerClient!.GetBlobClient(objectName);
                if (await blobClient.ExistsAsync(ct))
                {
                    var response = await blobClient.DownloadStreamingAsync(cancellationToken: ct);
                    return response.Value.Content;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading from Azure Blob: {ObjectName}", objectName);
            }
        }

        if (_cloudSync != null && _cloudSync.IsEnabled)
        {
            var remoteStream = await _cloudSync.DownloadRemotePhotoStreamAsync(objectName, ct);
            if (remoteStream != null) return remoteStream;
        }

        return _localFallback.OpenRead(objectName);
    }
}
