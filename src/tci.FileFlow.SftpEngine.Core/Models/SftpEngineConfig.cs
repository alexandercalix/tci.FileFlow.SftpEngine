using LiteDB;

namespace tci.FileFlow.SftpEngine.Core.Models;

public class SftpEngineConfig
{
    [BsonId]
    public int Id { get; set; } // Auto-incrementing key managed by LiteDB
    public bool IsActive { get; set; } // True = Active Production, False = Staging/Draft

    // Remote Server Parameters (SFTP)
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string RemoteFolder { get; set; } = "/upload";

    // Local Environment Parameters
    public string LocalSourceFolder { get; set; } = string.Empty;
    public int IntervalMinutes { get; set; } = 10;

    // Data Hygiene & Retention Rules
    public bool DeleteLocalAfterTransfer { get; set; }
    public bool MoveToBackupFolder { get; set; }
    public string BackupFolder { get; set; } = string.Empty;
    public bool MoveEmptyFiles { get; set; }
    public string EmptyFilesFolder { get; set; } = string.Empty;
    public int DeleteOlderThanDays { get; set; } = 7;

    // White-Label / Branding Personalization
    public string ClientName { get; set; } = string.Empty;

    // Authentication
    public string AdminPassword { get; set; } = "admin";
}