
using System.Net;
using AzuriteHub.Application.Interfaces;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Options;

namespace AzuriteHub.Infreastructure.Services;

public sealed class GoogleDriveStorage : ICloudStorage
{
    private readonly ILogger<GoogleDriveStorage> _logger;
    private readonly CloudStorageOptions _opts;
    private readonly DriveService _drive;


    public GoogleDriveStorage(
        ILogger<GoogleDriveStorage> logger,
        IOptions<CloudStorageOptions> options)
    {
        _logger = logger;
        _opts = options.Value;

        _drive = InitializeDriveService(_opts);
    }

    private DriveService InitializeDriveService(CloudStorageOptions opts)
    {
        switch (opts.GoogleDrive.Method)
        {
            case GoogleDriveOptions.GoogleDriveAccountMethod.ServiceAccount:

                return new DriveService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = GoogleCredential.FromFile(_opts.GoogleDrive.CredentialsJsonPath)
                    .CreateScoped(DriveService.Scope.DriveFile, DriveService.Scope.Drive),
                    ApplicationName = opts.GoogleDrive.ApplicationName
                });
            default:
                var secrets = GoogleClientSecrets.FromFile(_opts.GoogleDrive.CredentialsJsonPath).Secrets;
                var codeFlow = new GoogleAuthorizationCodeFlow(
                    new GoogleAuthorizationCodeFlow.Initializer
                    {
                        ClientSecrets = secrets,
                        Scopes = new[] { DriveService.Scope.Drive },
                        DataStore = new FileDataStore("Drive.Api.Auth.Store", true)
                    });

                var codeReceiver = new FixedPortCodeReceiver();
                return new DriveService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = new AuthorizationCodeInstalledApp(codeFlow, codeReceiver).AuthorizeAsync("user", CancellationToken.None).Result,
                    ApplicationName = opts.GoogleDrive.ApplicationName
                });
        }
    }

    public async Task<CloudUploadResult> UploadAsync(CloudUploadRequest request, CancellationToken ct)
    {
        try
        {
            if (!System.IO.File.Exists(request.LocalFilePath))
                return new CloudUploadResult(false, null, null, $"File not found: {request.LocalFilePath}");

            if (string.IsNullOrWhiteSpace(request.RemoteFileName))
                return new CloudUploadResult(false, null, null, "RemoteFileName is required.");

            _logger.LogInformation("Uploading to Google Drive: {RemoteFolderPath}/{RemoteFileName}", request.RemoteFolderPath, request.RemoteFileName);

            var folderId = await EnsureFolderAsync(request.RemoteFolderPath, ct);

            // Nếu overwrite thì xóa file cũ
            if (request.Overwrite)
            {
                var existing = await FindFileInFolderAsync(folderId, request.RemoteFileName, ct);
                if (existing is not null)
                {
                    var deleteRequest = _drive.Files.Delete(existing.Id);
                    deleteRequest.SupportsAllDrives = true;
                    await deleteRequest.ExecuteAsync(ct);
                    _logger.LogInformation("Overwrote existing file: {FileName}", request.RemoteFileName);
                }
            }

            var drive = _drive.Drives.Get(_opts.GoogleDrive.RootFolderId).Execute();
            Console.WriteLine(drive.Id);

            // Upload file
            var fileMetadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = request.RemoteFileName,
                Parents = new string[] { folderId }
            };

            await using var stream = System.IO.File.OpenRead(request.LocalFilePath);
            var upload = _drive.Files.Create(fileMetadata, stream, "");
            upload.Fields = "id";
            upload.SupportsAllDrives = true;
            var progress = upload.Upload();

            if (progress.Status == Google.Apis.Upload.UploadStatus.Completed && upload.ResponseBody != null)
            {
                var gfile = upload.ResponseBody;
                _logger.LogInformation("Upload completed. FileId: {Id}, ViewLink: {ViewLink}, DownloadLink: {DownloadLink}",
                    gfile.Id, gfile.WebViewLink, gfile.WebContentLink);

                return new CloudUploadResult(true, gfile.Id, gfile.WebViewLink ?? gfile.WebContentLink, "Uploaded");
            }

            _logger.LogError("Upload failed with status {Status}", progress.Status);
            _logger.LogError(progress.Exception, "Upload failed with error message {message}", progress.Exception.Message);
            return new CloudUploadResult(false, null, null, $"Upload failed: {progress.Status}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload failed for {LocalFile}", request.LocalFilePath);
            return new CloudUploadResult(false, null, null, ex.Message);
        }
    }

    public async Task<string> EnsureFolderAsync(string remoteFolderPath, CancellationToken ct)
    {
        string rootId = string.IsNullOrEmpty(_opts.GoogleDrive.RootFolderId) ? await EnsureRootAsync(ct) : _opts.GoogleDrive.RootFolderId;

        if (string.IsNullOrWhiteSpace(remoteFolderPath))
            return rootId;

        _logger.LogInformation("Start ensure with remotepath {remoteFolderPath}", remoteFolderPath);

        var segments = remoteFolderPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

        var currentId = rootId;

        foreach (var folderName in segments)
        {
            _logger.LogInformation("Start create with folder: {Name} ({Id})", folderName, currentId);

            var existingFolder = await FindFolderInFolderAsync(currentId, folderName, ct);
            if (existingFolder is null)
            {
                if (!_opts.CreateFoldersIfNotExist)
                    throw new DirectoryNotFoundException($"Folder '{folderName}' not found and auto-create disabled.");

                currentId = await CreateFolderAsync(currentId, folderName, ct);
                _logger.LogInformation("Created folder: {Name} ({Id})", folderName, currentId);
            }
            else
            {
                currentId = existingFolder.Id;
            }
        }

        return currentId;
    }

    public async Task DeleteAsync(string remoteIdOrPath, CancellationToken ct)
    {
        await _drive.Files.Delete(remoteIdOrPath).ExecuteAsync(ct);
        _logger.LogInformation("Deleted remote object: {Id}", remoteIdOrPath);
    }

    public async Task<string?> GetPublicUrlAsync(string remoteIdOrPath, CancellationToken ct)
    {
        try
        {
            var file = await _drive.Files.Get(remoteIdOrPath).ExecuteAsync(ct);

            // Nếu chưa có public link thì set permission
            if (string.IsNullOrEmpty(file.WebViewLink))
            {
                var perm = new Permission { Role = "reader", Type = "anyone" };
                await _drive.Permissions.Create(perm, remoteIdOrPath).ExecuteAsync(ct);

                file = await _drive.Files.Get(remoteIdOrPath).ExecuteAsync(ct);
            }

            return file.WebViewLink ?? file.WebContentLink;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetPublicUrl failed for {Id}", remoteIdOrPath);
            return null;
        }
    }

    private async Task<Google.Apis.Drive.v3.Data.File?> FindFolderInFolderAsync(string parentId, string name, CancellationToken ct)
        => await FindInFolderAsync(parentId, name, "application/vnd.google-apps.folder", ct);

    private async Task<Google.Apis.Drive.v3.Data.File?> FindFileInFolderAsync(string parentId, string name, CancellationToken ct)
        => await FindInFolderAsync(parentId, name, "not application/vnd.google-apps.folder", ct);

    private async Task<Google.Apis.Drive.v3.Data.File?> FindInFolderAsync(string parentId, string name, string mimeCondition, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(parentId) || parentId == ".")
            parentId = "root";


        var safeName = name.Replace("'", "\\'");
        var q = $"mimeType {(mimeCondition.StartsWith("not") ? "!=" : "=")} '{mimeCondition.Replace("not ", "")}' and name='{safeName}' and '{parentId}' in parents and trashed=false";

        var req = _drive.Files.List();
        req.Q = q;
        req.Fields = "files(id, name, webViewLink, webContentLink)";
        var res = await req.ExecuteAsync(ct);

        return res.Files?.FirstOrDefault();
    }

    private async Task<string> CreateFolderAsync(string parentId, string folderName, CancellationToken ct)
    {
        var folderMetadata = new Google.Apis.Drive.v3.Data.File
        {
            Name = folderName,
            MimeType = "application/vnd.google-apps.folder",
            Parents = new[] { parentId }
        };
        var req = _drive.Files.Create(folderMetadata);
        req.Fields = "id";
        var created = await req.ExecuteAsync(ct);
        return created.Id;
    }

    private async Task<string> EnsureRootAsync(CancellationToken ct)
    {
        const string root = "root"; // user's My Drive
        if (string.IsNullOrWhiteSpace(_opts.DefaultRootPath))
            return root;

        var existing = await FindFolderInFolderAsync(root, _opts.DefaultRootPath!, ct);
        return existing?.Id ?? await CreateFolderAsync(root, _opts.DefaultRootPath!, ct);
    }


}


public class FixedPortCodeReceiver : ICodeReceiver
{
    public string RedirectUri { get; } = "http://localhost:3030/authorize/";

    public async Task<AuthorizationCodeResponseUrl> ReceiveCodeAsync(
           AuthorizationCodeRequestUrl url,
           CancellationToken taskCancellationToken)
    {
        // Start HTTP listener on port 5109
        using (var http = new HttpListener())
        {
            http.Prefixes.Add("http://localhost:5109/authorize/");
            http.Start();

            // Redirect user
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url.Build().ToString(),
                UseShellExecute = true
            });

            // Waite google callback
            var context = await http.GetContextAsync();

            // Get code from query string
            var response = context.Response;
            var query = context.Request.Url.Query;
            var result = new AuthorizationCodeResponseUrl(query);

            // Response browser
            var responseString = "<html><body>Sign in with goole success.</body></html>";
            var buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();

            http.Stop();

            return result;
        }
    }
}