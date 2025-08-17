# Scheduled Jobs Overview
We are currently using Quartz.NET to manage scheduled jobs in the system.
The main scheduled job running is:
- `DatabaseBackupPipelineJob` – This job is responsible for:
- Backing up the database.
- Compressing the backup file.
- Uploading the compressed file to cloud storage.

# How to Modify the Schedule
If you need to change the execution schedule of the job:
- Go to:
```
Jobs/DependencyInjection.cs
```
- Update the `CronSchedule` configuration for the `DatabaseBackupPipelineJob`.

# Default Schedule
By default, the job is set to run **every Sunday at midnight**:
```
"0 0 0 ? * SUN"
```
This means the backup process will automatically run at **00:00 AM every Sunday**.