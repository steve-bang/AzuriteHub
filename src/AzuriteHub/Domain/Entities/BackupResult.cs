namespace AzuriteHub.Domain.Entities;

public class BackupResult
{
    public string FilePath { get; set; } = null!;
    public string Checksum { get; set; } = null!;
    public bool Success { get; set; }
    public DateTime CreatedAt { get; set; }

    public BackupResult(string filePath, string checksum, bool success)
    {
        FilePath = filePath;
        Checksum = checksum;
        Success = success;
        CreatedAt = DateTime.UtcNow;
    }
}