
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using tci.FileFlow.SftpEngine.Core.Models;
using tci.FileFlow.SftpEngine.Core.Services;

namespace tci.FileFlow.SftpEngine.Core.BackgroundServices;

public class FileFlowWorker : BackgroundService
{
    private readonly IDatabaseService _databaseService;
    private readonly ISftpService _sftpService;
    private readonly TransferProgressState _progressState;
    private readonly ILogger<FileFlowWorker> _logger;

    private const int BatchSize = 50; // Safeguard against memory spikes
    private DateTime _nextRunTime;
    private DateTime _nextPurgeTime;

    public FileFlowWorker(
        IDatabaseService databaseService,
        ISftpService sftpService,
        TransferProgressState progressState,
        ILogger<FileFlowWorker> logger)
    {
        _databaseService = databaseService;
        _sftpService = sftpService;
        _progressState = progressState;
        _logger = logger;

        // Initialize schedule markers
        var activeConfig = _databaseService.GetActiveConfig();
        _nextRunTime = DateTime.UtcNow.AddMinutes(activeConfig.IntervalMinutes);
        _nextPurgeTime = DateTime.UtcNow.AddDays(30); // Monthly database cleanup schedule
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FileFlow SFTP Engine Background Worker has started.");
        _progressState.IsWorkerRunning = true;

        // Modern 1-second high-resolution timer loop for crisp UI responsiveness
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (await timer.WaitForNextTickAsync(stoppingToken) && !stoppingToken.IsCancellationRequested)
        {
            try
            {
                var activeConfig = _databaseService.GetActiveConfig();
                var now = DateTime.UtcNow;

                // 1. Update UI Countdown Timers
                _progressState.TimeUntilNextRun = _nextRunTime > now ? _nextRunTime - now : TimeSpan.Zero;
                _progressState.TimeUntilNextDatabasePurge = _nextPurgeTime > now ? _nextPurgeTime - now : TimeSpan.Zero;
                _progressState.LastStatusUpdate = now;

                // 2. Execute Monthly Database Purge
                if (now >= _nextPurgeTime)
                {
                    _progressState.CurrentTaskName = "PURGING_DATABASE";
                    int deletedRecords = _databaseService.PurgeOldLogs(30);
                    _logger.LogInformation("Automated database clean up complete. Purged {Count} records.", deletedRecords);
                    _nextPurgeTime = now.AddDays(30);
                }

                // 3. Execute File Transfer Cycle (Triggered by Timer or Force Request)
                if (now >= _nextRunTime)
                {
                    await TriggerTransferCycleAsync(activeConfig, stoppingToken);
                    // Reset standard timing interval
                    _nextRunTime = DateTime.UtcNow.AddMinutes(activeConfig.IntervalMinutes);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled error occurred in the primary worker control loop.");
                _progressState.LastSystemError = ex.Message;
            }
            finally
            {
                _progressState.CurrentTaskName = "IDLE";
            }
        }

        _progressState.IsWorkerRunning = false;
        _logger.LogInformation("FileFlow SFTP Engine Background Worker has gracefully stopped.");
    }

    /// <summary>
    /// Public access point allowing the Blazor UI to bypass the execution timer and force a run immediately.
    /// </summary>
    public void ForceExecutionNow()
    {
        _nextRunTime = DateTime.UtcNow;
    }

    private async Task TriggerTransferCycleAsync(SftpEngineConfig config, CancellationToken stoppingToken)
    {
        _progressState.CurrentTaskName = "DIAGNOSTICS_HANDSHAKE";
        _logger.LogInformation("Starting automated SFTP transfer cycle.");

        // Validate local path existence on the host server
        if (!Directory.Exists(config.LocalSourceFolder))
        {
            _progressState.IsSftpConnectionOk = false;
            _progressState.LastSystemError = $"Local directory '{config.LocalSourceFolder}' does not exist on the host server.";
            _logger.LogError(_progressState.LastSystemError);
            return;
        }

        // Validate network connection to destination SFTP before pulling files
        var handshake = _sftpService.TestConnection(config);
        _progressState.IsSftpConnectionOk = handshake.Success;

        if (!handshake.Success)
        {
            _progressState.LastSystemError = $"SFTP Diagnostics Handshake Failed: {handshake.ErrorMessage}";
            _logger.LogWarning(_progressState.LastSystemError);

            // Log a collective connection failure record to history
            _databaseService.LogTransfer(new TransferLog
            {
                FileName = "N/A",
                Status = TransferStatus.ConnectionError,
                DiagnosticsMessage = handshake.ErrorMessage,
                ProcessedAt = DateTime.UtcNow
            });
            return;
        }

        // Connection is healthy, clear previous errors from the UI dashboard
        _progressState.LastSystemError = string.Empty;

        // 4. Memory-Safe Scanning using EnumerateFiles
        _progressState.CurrentTaskName = "SCANNING_DIRECTORY";

        var eligibleFiles = Directory.EnumerateFiles(config.LocalSourceFolder, "*.txt")
            .Select(filePath => new FileInfo(filePath))
            .Where(fileInfo =>
            {
                // Filter 1: Skip files older than 1 week entirely
                if (fileInfo.CreationTimeUtc < DateTime.UtcNow.AddDays(-7))
                {
                    return false;
                }

                // Filter 2: Handle empty files immediately by logging and skipping
                if (fileInfo.Length == 0)
                {
                    bool isAlreadyProcessed = _databaseService.IsFileAlreadyProcessed(fileInfo.Name);
                    string diagMessage = "Skipped processing: File is completely empty (0 bytes).";

                    if (config.MoveEmptyFiles && !string.IsNullOrWhiteSpace(config.EmptyFilesFolder))
                    {
                        try
                        {
                            if (!Directory.Exists(config.EmptyFilesFolder))
                            {
                                Directory.CreateDirectory(config.EmptyFilesFolder);
                            }

                            var emptyFilePath = Path.Combine(config.EmptyFilesFolder, fileInfo.Name);
                            if (File.Exists(emptyFilePath))
                            {
                                var extension = Path.GetExtension(fileInfo.Name);
                                var nameWithoutExt = Path.GetFileNameWithoutExtension(fileInfo.Name);
                                emptyFilePath = Path.Combine(config.EmptyFilesFolder, $"{nameWithoutExt}_{DateTime.Now:yyyyMMddHHmmss}{extension}");
                            }
                            File.Move(fileInfo.FullName, emptyFilePath);
                            diagMessage += " Moved to empty files folder.";
                        }
                        catch (Exception ex)
                        {
                            diagMessage += $" Failed to move: {ex.Message}";
                        }
                    }

                    if (!isAlreadyProcessed)
                    {
                        _databaseService.LogTransfer(new TransferLog
                        {
                            FileName = fileInfo.Name,
                            FileSizeInBytes = 0,
                            FileCreatedAt = fileInfo.CreationTimeUtc,
                            ProcessedAt = DateTime.UtcNow,
                            Status = TransferStatus.IgnoredEmpty,
                            DiagnosticsMessage = diagMessage
                        });
                    }
                    return false;
                }

                // Filter 3: Check database to ensure file hasn't been uploaded already
                return !_databaseService.IsFileAlreadyProcessed(fileInfo.Name);
            })
            .ToList();

        _progressState.TotalFilesToProcess = eligibleFiles.Count;
        _progressState.FilesSuccessfullyProcessed = 0;

        if (_progressState.TotalFilesToProcess == 0)
        {
            _logger.LogInformation("No new valid files found to process during this cycle.");
            return;
        }

        _progressState.CurrentTaskName = "TRANSFERRING_BATCHES";
        _logger.LogInformation("Found {Count} new files. Beginning batch processing loops.", _progressState.TotalFilesToProcess);

        int consecutiveErrors = 0;

        // 5. Stream Processing by Lots (Batching)
        for (int i = 0; i < _progressState.TotalFilesToProcess; i += BatchSize)
        {
            if (stoppingToken.IsCancellationRequested) break;

            var currentBatch = eligibleFiles.Skip(i).Take(BatchSize);

            foreach (var file in currentBatch)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    // Stream out bytes over the SFTP network socket
                    await _sftpService.TransferFileAsync(config, file.FullName, bytesUploaded =>
                    {
                        // Progress callback hooked to keep track of network progress if needed
                    });

                    // Log transactional success status to database
                    _databaseService.LogTransfer(new TransferLog
                    {
                        FileName = file.Name,
                        FileSizeInBytes = file.Length,
                        FileCreatedAt = file.CreationTimeUtc,
                        ProcessedAt = DateTime.UtcNow,
                        Status = TransferStatus.Transferred,
                        DiagnosticsMessage = "File successfully transferred and verified."
                    });

                    // Optional cleanup: Move to backup folder or delete
                    if (config.MoveToBackupFolder && !string.IsNullOrWhiteSpace(config.BackupFolder))
                    {
                        if (!Directory.Exists(config.BackupFolder))
                        {
                            Directory.CreateDirectory(config.BackupFolder);
                        }

                        var backupFilePath = Path.Combine(config.BackupFolder, file.Name);
                        if (File.Exists(backupFilePath))
                        {
                            var extension = Path.GetExtension(file.Name);
                            var nameWithoutExt = Path.GetFileNameWithoutExtension(file.Name);
                            backupFilePath = Path.Combine(config.BackupFolder, $"{nameWithoutExt}_{DateTime.Now:yyyyMMddHHmmss}{extension}");
                        }
                        File.Move(file.FullName, backupFilePath);
                    }
                    else if (config.DeleteLocalAfterTransfer)
                    {
                        File.Delete(file.FullName);
                    }

                    _progressState.FilesSuccessfullyProcessed++;
                    consecutiveErrors = 0; // Reset counter on success
                }
                catch (Exception ex)
                {
                    consecutiveErrors++;
                    _logger.LogError(ex, "Failed to transfer file: {FileName}", file.Name);
                    _progressState.LastSystemError = $"File upload error on {file.Name}: {ex.Message}";

                    _databaseService.LogTransfer(new TransferLog
                    {
                        FileName = file.Name,
                        FileSizeInBytes = file.Length,
                        FileCreatedAt = file.CreationTimeUtc,
                        ProcessedAt = DateTime.UtcNow,
                        Status = TransferStatus.TransferError,
                        DiagnosticsMessage = ex.Message
                    });

                    // Stop the current batch sequence immediately if connection breaks down mid-flight
                    // or if we encounter persistent/critical errors (like Permission denied)
                    var networkCheck = _sftpService.TestConnection(config);
                    if (!networkCheck.Success || consecutiveErrors >= 3 || ex.Message.IndexOf("Permission denied", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _progressState.IsSftpConnectionOk = false;
                        _logger.LogWarning("Aborting transfer cycle due to critical or persistent errors.");
                        return;
                    }
                }
            }
        }
    }
}