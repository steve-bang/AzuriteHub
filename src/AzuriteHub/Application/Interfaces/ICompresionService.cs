
namespace AzuriteHub.Application.Interfaces;

public interface ICompressionService
{
    Task<string> CompressAsync(
        string sourcePath,
        string? outputFileName = null,
        string? outputDir = null,
        CancellationToken cancellationToken =default
    );
}