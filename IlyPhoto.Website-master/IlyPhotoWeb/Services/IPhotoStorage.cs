namespace IlyPhotoWeb.Services;

public interface IPhotoStorage
{
    Task UploadAsync(string objectName, string contentType, Stream content, CancellationToken ct);
    Task DownloadToAsync(string objectName, Stream destination, CancellationToken ct);
    Task DeleteAsync(string objectName, CancellationToken ct);
    string GetReadUrl(string objectName, TimeSpan lifetime);
}
