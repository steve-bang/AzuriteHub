
namespace AzuriteHub.Application.Interfaces;

public interface ICloudStorage
{
    /// Upload a local file to a remote path (logical, provider-specific).
    /// Returns a remote file identifier (or URL) for later reference.
    Task<CloudUploadResult> UploadAsync(CloudUploadRequest request, CancellationToken ct = default);

    /// Ensure a remote folder path exists and return its provider-specific ID.
    Task<string> EnsureFolderAsync(string remoteFolderPath, CancellationToken ct);

    /// Delete a remote object by provider-specific ID or path (optional).
    Task DeleteAsync(string remoteIdOrPath, CancellationToken ct);

    /// Get a shareable link (if supported).
    Task<string?> GetPublicUrlAsync(string remoteIdOrPath, CancellationToken ct);
}



public sealed record CloudUploadRequest(
    string LocalFilePath,
    string RemoteFolderPath,   // logical path like "backups/sqlserver/2025/08"
    string RemoteFileName,     // e.g. "MyDb_20250816_010203.zip"
    string? ContentType = null,
    bool Overwrite = false
);

public sealed record CloudUploadResult(
    bool Success,
    string? RemoteId,
    string? RemoteUrl,
    string? Message
);