namespace ProjectWarehouse.Ops.Registry;

public interface IRegistryClient
{
    /// Tags of one repository, newest push first where the API exposes an order.
    Task<IReadOnlyList<string>> ListTagsAsync(string image, CancellationToken cancellationToken);
}

public sealed class RegistryException(string message, Exception? inner = null)
    : Exception(message, inner);
