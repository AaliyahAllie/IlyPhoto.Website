namespace IlyPhotoWeb.Services;

public class FirebaseOptions
{
    public string ProjectId { get; set; } = "";
    public string WebApiKey { get; set; } = "";
    public string CredentialsPath { get; set; } = "";
}

public class AzureStorageOptions
{
    /// <summary>Azure Storage Account connection string</summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>Azure Blob Container name for galleries</summary>
    public string ContainerName { get; set; } = "ilyphoto-galleries";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);
}

/// <summary>Where uploaded photos are saved on this computer when Azure is not active.</summary>
public class PhotoStorageOptions
{
    public string LocalPath { get; set; } = "App_Data/photos";
    /// <summary>Secret used to sign photo links.</summary>
    public string SigningKey { get; set; } = "";
}

public class AdminOptions
{
    public List<string> Emails { get; set; } = new();

    public bool IsAdminEmail(string email) =>
        Emails.Any(e => e.Trim().Equals(email.Trim(), StringComparison.OrdinalIgnoreCase));
}

public class BusinessOptions
{
    public string Name { get; set; } = "ILY Photo";
    public string Email { get; set; } = "info@ilyphoto.co.za";
    public string Phone { get; set; } = "+27 82 123 4567";
    public string BankDetails { get; set; } = "";
}
