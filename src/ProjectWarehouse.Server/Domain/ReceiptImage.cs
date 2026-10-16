using ProjectWarehouse.Server.Infrastructure.Files;

namespace ProjectWarehouse.Server.Domain;

public class ReceiptImage : IDataFileLink
{
    public Guid Id { get; set; }

    public Guid ReceiptId { get; set; }
    public Receipt Receipt { get; set; } = null!;

    public Guid DataFileId { get; set; }
    public DataFile DataFile { get; set; } = null!;

    public int Order { get; set; }
}
