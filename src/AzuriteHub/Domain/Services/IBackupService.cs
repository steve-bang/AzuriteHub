
using AzuriteHub.Domain.AggregateRoot;
using AzuriteHub.Domain.Entities;

namespace AzuriteHub.Domain.Services;

public interface IBackupService
{
    Task<BackupResult> BackupDatabaseAsync(string databaseName);

    Task<BackupResult> BackupDatabaseAsync(DatabaseConnection databaseConnection);
}

