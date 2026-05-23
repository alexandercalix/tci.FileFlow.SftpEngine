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
        active.MoveToBackupFolder = staging.MoveToBackupFolder;
        active.BackupFolder = staging.BackupFolder;
        active.MoveEmptyFiles = staging.MoveEmptyFiles;
        active.EmptyFilesFolder = staging.EmptyFilesFolder;
        active.DeleteOlderThanDays = staging.DeleteOlderThanDays;
        active.ClientName = staging.ClientName;

        col.Upsert(active);
        // We no longer delete the staging record so the draft form remains populated with the active parameters
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

    public TransferLogPage GetFilteredLogs(TransferLogFilter filter)
    {
        using var db = new LiteDatabase(_connectionString);
        var col = db.GetCollection<TransferLog>(LogCollection);

        var query = col.Query();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(filter.FileNameFilter))
        {
            var searchTerm = filter.FileNameFilter.ToLower();
            query = query.Where(x => x.FileName.ToLower().Contains(searchTerm));
        }

        if (filter.StatusFilter.HasValue)
        {
            query = query.Where(x => x.Status == filter.StatusFilter.Value);
        }

        if (filter.DateFromFilter.HasValue)
        {
            query = query.Where(x => x.ProcessedAt >= filter.DateFromFilter.Value);
        }

        if (filter.DateToFilter.HasValue)
        {
            var endOfDay = filter.DateToFilter.Value.AddDays(1).AddTicks(-1);
            query = query.Where(x => x.ProcessedAt <= endOfDay);
        }

        // Get total count before pagination
        var totalCount = query.Count();

        // Apply pagination
        var skip = (filter.PageNumber - 1) * filter.PageSize;
        var items = query
            .OrderByDescending(x => x.ProcessedAt)
            .Skip(skip)
            .Limit(filter.PageSize)
            .ToList();

        return new TransferLogPage
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = filter.PageNumber,
            PageSize = filter.PageSize
        };
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