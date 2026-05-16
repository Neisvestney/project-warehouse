namespace ProjectWarehouse.Server.Infrastructure;

public interface IHasIdentity
{
    public Guid Id { get; }
}

public interface IHasNullableIdentity
{
    public Guid? Id { get; }
}