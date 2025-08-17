
using System.Diagnostics;
using System.Security.Cryptography;
using AzuriteHub.Domain.AggregateRoot;
using AzuriteHub.Domain.Entities;
using AzuriteHub.Domain.Services;
using Microsoft.Extensions.Options;

namespace AzuriteHub.Infreastructure.Services;

public class SqlServerBackkupService(
    IOptions<List<DatabaseConnection>> options,
    ILogger<SqlServerBackkupService> logger
) : IBackupService
{
    private readonly List<DatabaseConnection> _databaseConnections = options.Value;

    public Task<BackupResult> BackupDatabaseAsync(string databaseName)
    {
        var db = _databaseConnections.FirstOrDefault(x => string.Equals(x.Name, databaseName, StringComparison.OrdinalIgnoreCase));
        if (db == null) throw new Exception("Database not found in config");

        return BackupDatabaseAsync(db);

    }

    public async Task<BackupResult> BackupDatabaseAsync(DatabaseConnection db)
    {
        try
        {
            // Get root path
            string rootPath = AppContext.BaseDirectory;

            string backupFile = db.BackupFolder.StartsWith(rootPath)
                    ? Path.Combine(db.BackupFolder, $"{db.Name}_{DateTime.Now:yyyyMMddHHmmss}.bak") :
                    Path.Combine(rootPath, db.BackupFolder, $"{db.Name}_{DateTime.Now:yyyyMMddHHmmss}.bak");

            if (!Directory.Exists(db.BackupFolder))
                Directory.CreateDirectory(db.BackupFolder);

            logger.LogInformation("Created directory {backupFile}", backupFile);

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "sqlcmd",
                Arguments = $"-Q \"BACKUP DATABASE [{db.Name}] TO DISK='{backupFile}'\" -S {db.Server} -U {db.Username} -P {db.Password}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            var process = new Process
            {
                StartInfo = processStartInfo
            };

            logger.LogInformation($"Command: {processStartInfo.FileName} {processStartInfo.Arguments}");

            logger.LogInformation("Start process backup database with database Name={name}, Disk={backupFile} Server={server}, Username={username}", db.Name, backupFile, db.Server, db.Username);
            process.Start();
            string stdOut = await process.StandardOutput.ReadToEndAsync();
            string stdErr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0 || !string.IsNullOrWhiteSpace(stdErr))
            {
                logger.LogError("SQLCMD backup failed. ExitCode={exitCode}, Error={error}", process.ExitCode, stdErr);
                throw new Exception($"SQLCMD backup failed: {stdErr}");
            }

            if (!File.Exists(backupFile))
            {
                logger.LogError($"Backup file not created: {backupFile}");
            }

            logger.LogInformation("Complete backup database");
            logger.LogInformation("Backup completed successfully at {file}", backupFile);

            var checksum = await CalculateChecksumAsync(backupFile);

            return new BackupResult(backupFile, checksum, success: true);
        }
        catch (Exception ex)
        {
            logger.LogError("Error backup database with database Name={name}, Server={server}, Username={username}", db.Name, db.Server, db.Username);
            logger.LogError(ex, ex.Message);
            throw;
        }
    }

    private async Task<string> CalculateChecksumAsync(string filePath)
    {
        using var sha = SHA256.Create();
        await using var stream = File.OpenRead(filePath);
        var hash = await sha.ComputeHashAsync(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}