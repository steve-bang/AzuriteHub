using AzuriteHub.Api.Extensions;
using AzuriteHub.Application.Interfaces;
using AzuriteHub.BuildingBlocks.Logger;
using AzuriteHub.Domain.Services;
using AzuriteHub.Infrastructure.Storage;
using AzuriteHub.Jobs;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder
    .AddLogger()
    .AddService()
    .AddJobs()
    .AddDatabaseConnection();

try
{
    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }


    app.UseHttpsRedirection();


    app.MapGet("/hello", () => "Hello world, We are AzuriteHub.").WithName("Hello");

    app.MapGet("/api/v1/backups/{name}", async (string name,
        [FromServices] ILogger<Program> logger,
        [FromServices] IBackupService backupService,
        [FromServices] ICompressionService compressionService,
        [FromServices] ICloudStorageFactory cloudStorageFactory
    ) =>
    {
        try
        {
            // 1. Backup database
            var result = await backupService.BackupDatabaseAsync(name);

            // 2. Compression file
            // Ex: C:\\_BACKUPS\\DB\\AzuriteHub_202502012021.zip
            string fileZipOutput = await compressionService.CompressAsync(
                result.FilePath
            );

            string remoteFolderPath = $"AzuriteHub/backups/sqlserver/{DateTime.UtcNow:yyyy/MM}"; 
            string remoteFileName = Path.GetFileName(fileZipOutput); // name file to save on cloud

            // 3. Upload on cloud
            var cloudStorage = cloudStorageFactory.Create();

            var uploadRequest = new CloudUploadRequest(
                LocalFilePath: fileZipOutput,
                RemoteFolderPath: remoteFolderPath,
                RemoteFileName: remoteFileName,
                ContentType: "application/zip",
                Overwrite: true
            );

            var uploadResult = await cloudStorage.UploadAsync(uploadRequest, default);

            // 5. Response the result
            return Results.Ok(new
            {
                Database = name,
                LocalBackup = result.FilePath,
                LocalZip = fileZipOutput,
                RemoteUrl = uploadResult.RemoteUrl
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Backup failed for {name}: {ex.Message}");
            return Results.Problem($"Backup failed for {name}: {ex.Message}");
        }
    }).WithName("Backup_By_Name");

    app.Run();

}
catch (Exception ex)
{
    Console.WriteLine("Error start application.");
    Console.WriteLine(ex);
}
finally
{

}
