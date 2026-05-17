namespace tci.FileFlow.SftpEngine.Core.Models;

public class TransferLogFilter
{
    public string? FileNameFilter { get; set; }
    public TransferStatus? StatusFilter { get; set; }
    public DateTime? DateFromFilter { get; set; }
    public DateTime? DateToFilter { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public class TransferLogPage
{
    public IEnumerable<TransferLog> Items { get; set; } = new List<TransferLog>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;
    public bool HasNextPage => PageNumber < TotalPages;
    public bool HasPreviousPage => PageNumber > 1;
}
