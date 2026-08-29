namespace ProjectWarehouse.Server.Infrastructure;

public class StoragePlaceNodeNotFoundException(Guid nodeId)
    : Exception($"Storage place node '{nodeId}' was not found."), IExpectedFailure
{
    public Guid NodeId { get; } = nodeId;
}
