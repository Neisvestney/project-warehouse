namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>
/// Marks an endpoint whose result depends on where the day boundary falls. The OpenAPI transformer
/// declares <c>X-Time-Zone</c> on every marked operation — a header set by an interceptor is invisible
/// in the generated client and in Scalar otherwise, and turns into word-of-mouth knowledge.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class TimeZoneAwareAttribute : Attribute;
