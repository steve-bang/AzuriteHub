
using System.IO.Compression;
using AzuriteHub.Application.Interfaces;

namespace AzuriteHub.Infreastructure.Services;

public class CompressionService(
    ILogger<CompressionService> logger
) : ICompressionService
{
    public async Task<string> CompressAsync(string sourceFile, string? outputFileName, string? outputDir, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Starting compression for source path: {sourceFile}", sourceFile);


            if (!File.Exists(sourceFile) && !Directory.Exists(sourceFile))
            {
                logger.LogError("Source path not found: {sourceFile}", sourceFile);
                throw new FileNotFoundException($"Source path not found: {sourceFile}");
            }

            // Xác định thư mục output
            string targetDir = string.IsNullOrWhiteSpace(outputDir)
                ? Path.GetDirectoryName(sourceFile)!
                : outputDir;

            if (!Directory.Exists(targetDir))
            {
                logger.LogInformation("Output directory does not exist, creating: {targetDir}", targetDir);
                Directory.CreateDirectory(targetDir);
            }

            // Xác định tên output file
            string baseName = !string.IsNullOrWhiteSpace(outputFileName)
                ? outputFileName
                : Path.GetFileNameWithoutExtension(sourceFile);


            string outputFile = Path.Combine(targetDir, $"{baseName}.zip");

            logger.LogInformation("Compressing {SourcePath} into {outputFile}", sourceFile, outputFile);

            await Task.Run(() =>
            {
                if (Directory.Exists(sourceFile))
                {
                    // Trường hợp source là folder
                    ZipFile.CreateFromDirectory(sourceFile, outputFile, CompressionLevel.Fastest, includeBaseDirectory: true);
                }
                else
                {
                    // Trường hợp source là file
                    using (var archive = ZipFile.Open(outputFile, ZipArchiveMode.Create))
                    {
                        string entryName = Path.GetFileName(sourceFile);
                        archive.CreateEntryFromFile(sourceFile, entryName, CompressionLevel.Fastest);
                    }
                }
            }, cancellationToken);

            logger.LogInformation("Compression completed successfully. Output file: {outputFile}", outputFile);

            return outputFile;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Compression was canceled for source path: {sourceFile}", sourceFile);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while compressing {sourceFile}", sourceFile);
            throw;
        }
    }
}