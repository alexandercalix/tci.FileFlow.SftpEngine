using LiteDB;
using tci.FileFlow.SftpEngine.Core.Models;

namespace tci.FileFlow.SftpEngine.Core.Services;

public class DatabaseService : IDatabaseService
{
    private readonly string _connectionString = "Filename=DataMover.db;Connection=shared";
    private const string ConfigCollection = "configuration";
    private const string LogCollection = "transfer_logs";

    public SftpEngineConfig GetActiveConfig()
    {
        using var db = new LiteDatabase(_connectionString);
        var col = db.GetCollection<SftpEngineConfig>(ConfigCollection);

        var config = col.FindOne(x => x.IsActive);
        if (config == null)
        {
            config = new SftpEngineConfig { IsActive = true };
            col.Insert(config);
        }
        return config;
    }

    public SftpEngineConfig GetStagingConfig()
    {
        using var db = new LiteDatabase(_connectionString);
        var col = db.GetCollection<SftpEngineConfig>(ConfigCollection);

        var config = col.FindOne(x => !x.IsActive);
        if (config == null)
        {
            config = new SftpEngineConfig { IsActive = false };
            col.Insert(config);
        }
        return config;
    }

    public void SaveConfig(SftpEngineConfig config)
    {
        using var db = new LiteDatabase(_connectionString);
        var col = db.GetCollection<SftpEngineConfig>(ConfigCollection);
        col.Upsert(config);
    }

    public void ApplyStagingConfig()
    {
        using var db = new LiteDatabase(_connectionString);
        var col = db.GetCollection<SftpEngineConfig>(ConfigCollection);

        var staging = col.FindOne(x => !x.IsActive);
        if (staging == null) return;

        var active = col.FindOne(x => x.IsActive) ?? new SftpEngineConfig { IsActive = true };

        // Map staging fields onto the single active record (excluding Logo)
        active.Host = staging.Host;
        active.Port = staging.Port;
        active.Username = staging.Username;
        active.Password = staging.Password;
        active.RemoteFolder = staging.RemoteFolder;
        active.LocalSourceFolder = staging.LocalSourceFolder;
        active.IntervalMinutes = staging.IntervalMinutes;
        active.DeleteLocalAfterTransfer = staging.DeleteLocalAfterTransfer;
        active.DeleteOlderThanDays = staging.DeleteOlderThanDays;
        active.ClientName = staging.ClientName;

        col.Upsert(active);
        col.Delete(staging.Id);
    }

    public bool IsFileAlreadyProcessed(string fileName)
    {
        using var db = new LiteDatabase(_connectionString);
        var col = db.GetCollection<TransferLog>(LogCollection);

        return col.Exists(x => x.FileName == fileName &&
                              (x.Status == TransferStatus.Transferred ||
                               x.Status == TransferStatus.IgnoredEmpty));
    }

    public void LogTransfer(TransferLog log)
    {
        using var db = new LiteDatabase(_connectionString);
        var col = db.GetCollection<TransferLog>(LogCollection);

        col.EnsureIndex(x => x.FileName);
        col.Insert(log);
    }

    public IEnumerable<TransferLog> GetRecentLogs(int limit = 100)
    {
        using var db = new LiteDatabase(_connectionString);
        var col = db.GetCollection<TransferLog>(LogCollection);

        return col.Query()
            .OrderByDescending(x => x.ProcessedAt)
            .Limit(limit)
            .ToList();
    }

    public int PurgeOldLogs(int daysOld = 30)
    {
        using var db = new LiteDatabase(_connectionString);
        var col = db.GetCollection<TransferLog>(LogCollection);

        var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
        return col.DeleteMany(x => x.ProcessedAt < cutoffDate);
    }

    public void ResetProcessedLogs()
    {
        using var db = new LiteDatabase(_connectionString);
        db.DropCollection(LogCollection);
    }
}