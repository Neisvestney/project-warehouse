namespace ProjectWarehouse.Server.Domain;

public class ChangeLogDiff
{
    public string Path { get; set; } = null!;
    public object? From { get; set; }
    public object? To { get; set; }
}
