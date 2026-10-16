namespace ProjectWarehouse.Server.Domain;

public class WriteoffTag : Tag
{
    public ICollection<Writeoff> Writeoffs { get; set; } = [];
}
