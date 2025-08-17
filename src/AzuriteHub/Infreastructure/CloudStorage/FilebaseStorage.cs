using AzuriteHub.Application.Interfaces;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;
public sealed class FirebaseStorage : ICloudStorage
{
    private readonly ILogger<FirebaseStorage> _logger;
    private readonly CloudStorageOptions _opts;
    private readonly StorageClient _storage;

    public FirebaseStorage(
        ILogger<FirebaseStorage> logger,
        IOptions<CloudStorageOptions> options)
    {
        _logger = logger;
        _opts = options.Value;

        var credential = GoogleCredential.FromFile(_opts.Firebase.CredentialsJsonPath);
        _storage = StorageClient.Create(credential);
    }

    public async Task<CloudUploadResult> UploadAsync(CloudUploadRequest request, CancellationToken ct)
    {
        try
        {
            if (!File.Exists(request.LocalFilePath))
                return new CloudUploadResult(false, null, null, $"File not found: {request.LocalFilePath}");

            if (string.IsNullOrWhiteSpace(request.RemoteFileName))
                return new CloudUploadResult(false, null, null, "RemoteFileName is required.");

            string objectName = string.IsNullOrWhiteSpace(request.RemoteFolderPath)
                ? request.RemoteFileName
                : $"{request.RemoteFolderPath.TrimEnd('/')}/{request.RemoteFileName}";

            _logger.LogInformation("Uploading to Firebase Storage: {ObjectName}", objectName);

            using var stream = File.OpenRead(request.LocalFilePath);
            var obj = await _storage.UploadObjectAsync(_opts.Firebase.BucketName, objectName, request.ContentType ?? "application/octet-stream", stream, cancellationToken: ct);

            _logger.LogInformation("Upload completed. Object: {Name}", obj.Name);

            // Create public link
            string publicUrl = $"https://firebasestorage.googleapis.com/v0/b/{_opts.Firebase.BucketName}/o/{Uri.EscapeDataString(obj.Name)}?alt=media";

            return new CloudUploadResult(true, obj.Name, publicUrl, "Uploaded");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload failed for {LocalFile}", request.LocalFilePath);
            return new CloudUploadResult(false, null, null, ex.Message);
        }
    }

    public async Task DeleteAsync(string remoteIdOrPath, CancellationToken ct)
    {
        try
        {
            await _storage.DeleteObjectAsync(_opts.Firebase.BucketName, remoteIdOrPath, cancellationToken: ct);
            _logger.LogInformation("Deleted remote object: {Id}", remoteIdOrPath);
        }
        catch (Google.GoogleApiException e) when (e.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Object {Id} not found for deletion", remoteIdOrPath);
        }
    }

    public Task<string?> GetPublicUrlAsync(string remoteIdOrPath, CancellationToken ct)
    {
        string publicUrl = $"https://firebasestorage.googleapis.com/v0/b/{_opts.Firebase.BucketName}/o/{Uri.EscapeDataString(remoteIdOrPath)}?alt=media";
        return Task.FromResult<string?>(publicUrl);
    }

    public Task<string> EnsureFolderAsync(string remoteFolderPath, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
