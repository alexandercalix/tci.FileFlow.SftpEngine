using tci.FileFlow.SftpEngine.Core.Models;

namespace tci.FileFlow.SftpEngine.Core.Services;

public interface IDatabaseService
{
    // Configuration Management (Staging Pattern)
    SftpEngineConfig GetActiveConfig();
    SftpEngineConfig GetStagingConfig();
    void SaveConfig(SftpEngineConfig config);
    void ApplyStagingConfig();

    // File Processing & Log Audit
    bool IsFileAlreadyProcessed(string fileName);
    void LogTransfer(TransferLog log);
    IEnumerable<TransferLog> GetRecentLogs(int limit = 100);
    TransferLogPage GetFilteredLogs(TransferLogFilter filter);

    // Database Hygiene
    int PurgeOldLogs(int daysOld = 30);
    void ResetProcessedLogs(); // Used when the user changes destination servers
}