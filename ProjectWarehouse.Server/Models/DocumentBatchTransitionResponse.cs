namespace ProjectWarehouse.Server.Models;

/// <summary>Result of a module's batch transition endpoint: each document is transitioned on its own.</summary>
public class DocumentBatchTransitionResponse
{
    public IReadOnlyList<Guid> TransitionedIds { get; init; } = [];
    public IReadOnlyList<DocumentBatchTransitionFailedItem> FailedItems { get; init; } = [];
}

public class DocumentBatchTransitionFailedItem
{
    public Guid Id { get; init; }
    /// <summary>Null when the document could not be loaded — missing or outside the caller's edit access.</summary>
    public int? Number { get; init; }
    public required AppFieldError Error { get; init; }

    /// <summary>Carries the first error of <paramref name="problem"/>; a transition rejects with a single one.</summary>
    public static DocumentBatchTransitionFailedItem From(Guid id, int? number, AppProblemDetails problem) => new()
    {
        Id     = id,
        Number = number,
        Error  = problem.Errors.Values.First()[0],
    };
}
