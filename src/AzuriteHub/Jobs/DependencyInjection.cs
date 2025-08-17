

using Quartz;

namespace AzuriteHub.Jobs;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddJobs(this IHostApplicationBuilder builder)
    {
        builder.Services.AddQuartz(q =>
        {

            var jobKey = new JobKey("DatabaseBackupPipelineJob");
            q.AddJob<DatabaseBackupPipelineJob>(opts => opts.WithIdentity(jobKey));

            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity("DatabaseBackupPipelineJob-trigger")
                .WithCronSchedule("0 0 0 ? * SUN") // Midnight SUN
            );
        });

        builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

        return builder;
    }
}