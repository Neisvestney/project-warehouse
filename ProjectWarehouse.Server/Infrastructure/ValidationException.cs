namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>
/// Thrown by services when a domain validation rule is violated
/// (e.g. duplicate inventory number). Carries enough information for a controller
/// to convert it directly into a 422 <c>AppProblemDetails</c> field error.
/// </summary>
public class ValidationException(string field, ErrorCode errorCode, string message)
    : Exception(message)
{
    /// <summary>The request field the error should be attached to.</summary>
    public string Field { get; } = field;

    /// <summary>Machine-readable error code.</summary>
    public ErrorCode ErrorCode { get; } = errorCode;

    /// <summary>
    /// Returns a new <see cref="ValidationException"/> with <paramref name="prefix"/> prepended to <see cref="Field"/>
    /// (e.g. <c>prefix="components[0]"</c> + <c>field="inventoryNumber"</c> → <c>"components[0].inventoryNumber"</c>).
    /// </summary>
    public ValidationException WithPrefix(string prefix) =>
        new($"{prefix}.{Field}", ErrorCode, Message);
}
