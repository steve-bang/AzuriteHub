
using AzuriteHub.Domain.Constants;

namespace AzuriteHub.Domain.AggregateRoot;

public class DatabaseConnection
{
    public string Name { get; private set; }
    public DatabaseProvider Provider { get; private set; }
    public string Server { get; private set; }
    public string Username { get; private set; }
    public string Password { get; private set; }
    public string BackupFolder { get; private set; }


    public DatabaseConnection(string name, DatabaseProvider provider, string server, string username, string password, string backupFolder)
    {
        Name = name;
        Provider = provider;
        Server = server;
        Username = username;
        Password = password;
        BackupFolder = backupFolder;
    }
}