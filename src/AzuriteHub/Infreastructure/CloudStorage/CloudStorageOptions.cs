using AzuriteHub.Domain.Constants;

public sealed class CloudStorageOptions
{
    public CloudProvider Provider { get; set; } = CloudProvider.GoogleDrive;

    // Common
    public bool CreateFoldersIfNotExist { get; set; } = true;
    public string? DefaultRootPath { get; set; } = "azuritehub";

    // Google Drive specific
    public GoogleDriveOptions GoogleDrive { get; set; } = new();

    public FirebaseStorageOptions Firebase { get; set; } = new();
}

public sealed class GoogleDriveOptions
{
    public enum GoogleDriveAccountMethod { ServiceAccount, OAuth2 }

    public GoogleDriveAccountMethod Method { get; set; }

    // Path to service account JSON or OAuth client secret (service account recommended for server jobs)
    public string CredentialsJsonPath { get; set; } = "";
    public string ApplicationName { get; set; } = "AzuriteHub";
    // Optional: If you already know a fixed root folder ID
    public string? RootFolderId { get; set; }
}

public sealed class FirebaseStorageOptions
{
    // Path to service account JSON or OAuth client secret (service account recommended for server jobs)
    public string CredentialsJsonPath { get; set; } = "";
    public string BucketName { get; set; }
}