
using AzuriteHub.Application.Interfaces;
using AzuriteHub.Domain.AggregateRoot;
using AzuriteHub.Domain.Services;
using AzuriteHub.Infrastructure.Storage;
using AzuriteHub.Infreastructure.Services;

namespace AzuriteHub.Api.Extensions;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddDatabaseConnection(this IHostApplicationBuilder builder)
    {
        var db = builder.Configuration.GetSection("Resource:Database");

        builder.Services.AddOptions<List<DatabaseConnection>>()
            .Bind(db)
            .ValidateOnStart();

        return builder;
    }


    public static IHostApplicationBuilder AddService(this IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<IBackupService, SqlServerBackkupService>();

        builder.Services.AddScoped<ICompressionService, CompressionService>();

        builder.Services.AddCloudStorage(builder.Configuration);

        return builder;
    }

    public static IServiceCollection AddCloudStorage(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<CloudStorageOptions>(config.GetSection("CloudStorage"));
        services.AddSingleton<GoogleDriveStorage>(); 
        services.AddSingleton<ICloudStorageFactory, CloudStorageFactory>();

        // Optional: if you often just need one active storage instance:
        services.AddScoped<ICloudStorage>(sp => sp.GetRequiredService<ICloudStorageFactory>().Create());

        return services;
    }
}