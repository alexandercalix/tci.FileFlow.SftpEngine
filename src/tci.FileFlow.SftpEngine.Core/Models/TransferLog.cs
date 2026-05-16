using LiteDB;

namespace tci.FileFlow.SftpEngine.Core.Models;

public enum TransferStatus
{
    Transferred,
    IgnoredEmpty,
    IgnoredByAge,
    ConnectionError,
    TransferError
}

public class TransferLog
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    public string FileName { get; set; } = string.Empty;
    public long FileSizeInBytes { get; set; }
    public DateTime FileCreatedAt { get; set; }
    public DateTime ProcessedAt { get; set; }
    public TransferStatus Status { get; set; }
    public string DiagnosticsMessage { get; set; } = "OK";
}