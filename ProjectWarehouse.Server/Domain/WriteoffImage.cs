using ProjectWarehouse.Server.Infrastructure.Files;

namespace ProjectWarehouse.Server.Domain;

public class WriteoffImage : IDataFileLink
{
    public Guid Id { get; set; }

    public Guid WriteoffId { get; set; }
    public Writeoff Writeoff { get; set; } = null!;

    public Guid DataFileId { get; set; }
    public DataFile DataFile { get; set; } = null!;

    public int Order { get; set; }
}
