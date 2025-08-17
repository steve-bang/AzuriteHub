// AzuriteHub.Infrastructure/Storage/CloudStorageFactory.cs
using AzuriteHub.Application.Interfaces;
using AzuriteHub.Domain.Constants;
using AzuriteHub.Infreastructure.Services;
using Microsoft.Extensions.Options;

namespace AzuriteHub.Infrastructure.Storage;

public interface ICloudStorageFactory
{
    ICloudStorage Create();
}

public sealed class CloudStorageFactory : ICloudStorageFactory
{
    private readonly IServiceProvider _sp;
    private readonly IOptions<CloudStorageOptions> _opts;

    public CloudStorageFactory(IServiceProvider sp, IOptions<CloudStorageOptions> opts)
    {
        _sp = sp; _opts = opts;
    }

    public ICloudStorage Create() =>
        _opts.Value.Provider switch
        {
            CloudProvider.GoogleDrive => _sp.GetRequiredService<GoogleDriveStorage>(),
            // CloudProvider.AwsS3 => _sp.GetRequiredService<AwsS3Storage>(),
            // CloudProvider.AzureBlob => _sp.GetRequiredService<AzureBlobStorage>(),
            // CloudProvider.FirebaseStorage => _sp.GetRequiredService<FirebaseStorageService>(),
            _ => throw new NotSupportedException($"Provider '{_opts.Value.Provider}' not supported")
        };
}
