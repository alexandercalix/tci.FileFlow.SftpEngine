using Renci.SshNet;
using tci.FileFlow.SftpEngine.Core.Models;

namespace tci.FileFlow.SftpEngine.Core.Services;

public class SftpService : ISftpService
{
    public (bool Success, string ErrorMessage) TestConnection(SftpEngineConfig config)
    {
        try
        {
            var connectionInfo = new ConnectionInfo(
                config.Host,
                config.Port,
                config.Username,
                new PasswordAuthenticationMethod(config.Username, config.Password)
            )
            {
                Timeout = TimeSpan.FromSeconds(10) // Fast timeout for immediate UI feedback
            };

            using var client = new SftpClient(connectionInfo);
            client.Connect();

            // Check if the configured remote directory actually exists on the target
            if (!client.Exists(config.RemoteFolder))
            {
                return (false, $"The remote directory '{config.RemoteFolder}' does not exist on the server.");
            }

            client.Disconnect();
            return (true, "OK");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task TransferFileAsync(SftpEngineConfig config, string localFilePath, Action<long> onBytesUploaded)
    {
        if (!File.Exists(localFilePath))
        {
            throw new FileNotFoundException("The local source file was not found.", localFilePath);
        }

        var connectionInfo = new ConnectionInfo(
            config.Host,
            config.Port,
            config.Username,
            new PasswordAuthenticationMethod(config.Username, config.Password)
        )
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        using var client = new SftpClient(connectionInfo);

        // Connect to the remote server asynchronously
        await Task.Run(() => client.Connect());

        var fileName = Path.GetFileName(localFilePath);
        var remoteDestinationPath = Path.Combine(config.RemoteFolder, fileName).Replace("\\", "/");

        // Open read stream from the local host file system
        using var localFileStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        // Upload the file streaming bytes directly across the network socket
        await Task.Run(() =>
        {
            client.UploadFile(localFileStream, remoteDestinationPath, uploadProgress =>
            {
                // Push total uploaded bytes back upstream to update the Blazor progress state bar
                onBytesUploaded((long)uploadProgress);
            });
        });

        await Task.Run(() => client.Disconnect());
    }
}