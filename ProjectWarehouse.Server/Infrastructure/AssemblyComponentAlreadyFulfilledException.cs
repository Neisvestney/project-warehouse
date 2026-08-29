namespace ProjectWarehouse.Server.Infrastructure;

public class AssemblyComponentAlreadyFulfilledException(Guid componentId)
    : Exception($"AssemblyTaskBoxComponent '{componentId}' is already fully fulfilled."), IExpectedFailure
{
    public Guid ComponentId { get; } = componentId;
}
