namespace tci.FileFlow.SftpEngine.Core.Models;

public class TransferProgressState
{
    public string CurrentTaskName { get; set; } = "IDLE";
    public bool IsWorkerRunning { get; set; }
    public bool IsSftpConnectionOk { get; set; } = true;

    // Batch Metrics for RAM-Safe Operations
    public int TotalFilesToProcess { get; set; }
    public int FilesSuccessfullyProcessed { get; set; }

    // Live percentage calculation for the Blazor Progress Bar
    public int CurrentBatchPercentage => TotalFilesToProcess > 0
        ? (FilesSuccessfullyProcessed * 100) / TotalFilesToProcess
        : 0;

    // Countdowns for Dashboard UI
    public TimeSpan TimeUntilNextRun { get; set; }
    public TimeSpan TimeUntilNextDatabasePurge { get; set; }

    // Live Diagnostics Stream
    public string LastSystemError { get; set; } = string.Empty;
    public DateTime LastStatusUpdate { get; set; } = DateTime.UtcNow;
}