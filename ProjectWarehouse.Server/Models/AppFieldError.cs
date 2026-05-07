using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models;

public class AppFieldError
{
    public ErrorCode Code { get; init; }
    public string Detail { get; init; } = string.Empty;
}
