using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.Files;
using ProjectWarehouse.Server.Models;

namespace ProjectWarehouse.Server.Services;

public class DataFileBindingService(ApplicationDbContext db, IListUpdater listUpdater) : IDataFileBindingService
{
    public async Task<AppProblemDetails?> BindSingleAsync(
        Guid? fileId, Action<Guid?> assign, string field, CancellationToken ct)
    {
        var problem = fileId is { } id
            ? await ValidateExistAsync([id], field, ct)
            : null;

        if (problem is not null) return problem;

        assign(fileId);
        return null;
    }

    public async Task<AppProblemDetails?> BindListAsync<TLink, TRequest>(
        IReadOnlyList<TRequest> requests,
        List<TLink> links,
        DbSet<TLink> dbSet,
        Action<TLink> setOwner,
        string field,
        CancellationToken ct)
        where TLink : class, IDataFileLink
        where TRequest : class, IDataFileLinkRequest
    {
        var problem = await ValidateExistAsync(requests.Select(r => r.FileId), field, ct);
        if (problem is not null) return problem;

        listUpdater.UpdateList(
            requests.ToList(),
            links,
            dbSet,
            compare: (link, request) => request.Id is { } id && link.Id == id,
            isNew: request => request.Id is null,
            afterMap: (request, link) =>
            {
                link.DataFileId = request.FileId;
                link.Order = request.Order;
                setOwner(link);
            });

        return null;
    }

    private async Task<AppProblemDetails?> ValidateExistAsync(
        IEnumerable<Guid> fileIds, string field, CancellationToken ct)
    {
        var wanted = fileIds.Distinct().ToList();
        if (wanted.Count == 0) return null;

        var found = await db.DataFiles.CountAsync(f => wanted.Contains(f.Id), ct);
        if (found == wanted.Count) return null;

        return AppProblems.UnprocessableEntity(field, ErrorCode.DataFileNotFound,
            "Referenced file does not exist. It may have been collected after the form was left open.");
    }
}
