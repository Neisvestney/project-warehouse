using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ProjectWarehouse.Server.Infrastructure.Observability;

/// <summary>
/// Runs a unit of work inside a database transaction covered by a span on
/// <see cref="AppTelemetry.ActivitySourceName" />.
/// <para>
/// EF Core itself emits no transaction span — the instrumentation library only turns command diagnostics
/// into spans. The span starts in the caller's frame, so <see cref="Activity.Current" /> flows downstream
/// and the command spans nest inside it.
/// </para>
/// </summary>
public static class TransactionTracing
{
    /// <summary>
    /// Opens a transaction, runs <paramref name="action" /> and commits; any failure leaves the transaction to
    /// roll back on dispose and propagates.
    /// <para>
    /// The transaction is opened directly. Turning on <c>EnableRetryOnFailure</c> makes every call here throw
    /// until the unit of work is made retry-safe and wrapped in <c>CreateExecutionStrategy</c> again.
    /// </para>
    /// <para>
    /// <paramref name="operation" /> is the span suffix identifying the unit of work, e.g.
    /// <c>receipts.placement.standard</c>.
    /// </para>
    /// </summary>
    public static async Task ExecuteInTransactionAsync(
        this DatabaseFacade database,
        string operation,
        Func<Task> action,
        CancellationToken ct = default)
    {
        using var activity = AppTelemetry.Source.StartActivity($"db.transaction {operation}", ActivityKind.Client);

        try
        {
            await using var tx = await database.BeginTransactionAsync(ct);
            await action();
            await tx.CommitAsync(ct);
            activity?.SetTag("db.transaction.outcome", "commit");
        }
        catch (OperationCanceledException)
        {
            activity?.SetTag("db.transaction.outcome", "cancelled");
            throw;
        }
        catch (Exception e)
        {
            activity?.SetTag("db.transaction.outcome", "rollback");
            activity?.AddException(e);
            activity?.SetStatus(ActivityStatusCode.Error, e.Message);
            throw;
        }
    }

    /// <inheritdoc cref="ExecuteInTransactionAsync(DatabaseFacade,string,Func{Task},CancellationToken)" />
    public static async Task<T> ExecuteInTransactionAsync<T>(
        this DatabaseFacade database,
        string operation,
        Func<Task<T>> action,
        CancellationToken ct = default)
    {
        var result = default(T)!;
        await database.ExecuteInTransactionAsync(operation, async () => result = await action(), ct);
        return result;
    }
}
