using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models;

public class AppEntity
{
    public Guid? Id {get; set;}
    public string? Name {get; set;}
    public AppEntityType Type {get; set;}
    public IReadOnlyDictionary<string, object>? AdditionalFields { get; init; }
}