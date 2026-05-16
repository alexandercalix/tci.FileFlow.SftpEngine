using tci.FileFlow.SftpEngine.Core.Models;

namespace tci.FileFlow.SftpEngine.Core.Services;

public interface ISftpService
{
    /// <summary>
    /// Executes a quick handshake diagnostics test against the provided configuration parameters.
    /// </summary>
    /// <returns>A tuple indicating success status and an error message if any.</returns>
    (bool Success, string ErrorMessage) TestConnection(SftpEngineConfig config);

    /// <summary>
    /// Streams a local file over the network to the configured SFTP destination server.
    /// </summary>
    Task TransferFileAsync(SftpEngineConfig config, string localFilePath, Action<long> onBytesUploaded);
}