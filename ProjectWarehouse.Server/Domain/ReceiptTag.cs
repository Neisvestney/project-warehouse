namespace ProjectWarehouse.Server.Domain;

public class ReceiptTag : Tag
{
    public ICollection<Receipt> Receipts { get; set; } = [];
}
