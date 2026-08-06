using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Infrastructure.Files;
using ProjectWarehouse.Server.Models;

namespace ProjectWarehouse.Server.Services;

/// <summary>
/// The single way controllers attach files to entities. Both methods return null on success and an
/// <see cref="AppProblemDetails"/> to hand to <c>Problem(...)</c> otherwise.
/// </summary>
/// <remarks>
/// Existence is checked explicitly rather than left to the foreign key: a raw 23503 surfaces as a
/// 500, while the frontend can only render an AppProblemDetails. This is also where a form left
/// open past OrphanTtlHours lands — as a clean 422 rather than a crash.
/// </remarks>
public interface IDataFileBindingService
{
    /// <summary>Validates a single optional file reference and assigns it to the owner.</summary>
    Task<AppProblemDetails?> BindSingleAsync(
        Guid? fileId, Action<Guid?> assign, string field, CancellationToken ct);

    /// <summary>
    /// Validates every referenced file and syncs the join rows through <c>IListUpdater</c>.
    /// <c>setOwner</c> sets the owner foreign key on each link the updater created or matched.
    /// Requires an AutoMapper map from <typeparamref name="TRequest"/> to <typeparamref name="TLink"/>
    /// with <c>Id</c> ignored — the list updater creates new entities through the mapper.
    /// </summary>
    Task<AppProblemDetails?> BindListAsync<TLink, TRequest>(
        IReadOnlyList<TRequest> requests,
        List<TLink> links,
        DbSet<TLink> dbSet,
        Action<TLink> setOwner,
        string field,
        CancellationToken ct)
        where TLink : class, IDataFileLink
        where TRequest : class, IDataFileLinkRequest;
}
