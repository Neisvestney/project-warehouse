using ProjectWarehouse.Server.Infrastructure.Files;

namespace ProjectWarehouse.Server.Domain;

public class StocktakeImage : IDataFileLink
{
    public Guid Id { get; set; }

    public Guid StocktakeId { get; set; }
    public Stocktake Stocktake { get; set; } = null!;

    public Guid DataFileId { get; set; }
    public DataFile DataFile { get; set; } = null!;

    public int Order { get; set; }
}
