
using AzuriteHub.Application.Interfaces;
using AzuriteHub.Domain.AggregateRoot;
using AzuriteHub.Domain.Services;
using Microsoft.Extensions.Options;
using Quartz;

namespace AzuriteHub.Jobs;

public class DatabaseBackupPipelineJob(
    IBackupService backupService,
    ICompressionService compressionService,
    IOptions<List<DatabaseConnection>> options,
    ILogger<DatabaseBackupPipelineJob> logger
) : IJob
{
    private readonly List<DatabaseConnection> DatabaseConnections = options.Value;

    /// <summary>
    /// Quartz job execution entry point.
    /// This pipeline performs: Database Backup → Compression → (future) Upload
    /// </summary>
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("DatabaseBackupPipelineJob started at {StartTime}", DateTime.UtcNow);

        if (DatabaseConnections != null && DatabaseConnections.Count > 0)
        {
            foreach (var db in DatabaseConnections)
            {
                try
                {
                    logger.LogInformation("Starting backup for database: {name}", db.Name);
                    
                    // Backup db
                    var backupResult = await backupService.BackupDatabaseAsync(db);

                    // Zip file
                    var compressedFile = await compressionService.CompressAsync(backupResult.FilePath);

                    logger.LogInformation("Compression completed for {name}. Compressed file: {compressedFile}", db.Name, compressedFile);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while processing database {DatabaseName}", db.Name);
                }
            }
        }

        logger.LogInformation("DatabaseBackupPipelineJob finished at {EndTime}", DateTime.UtcNow);
    }


}
