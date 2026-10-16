using Microsoft.AspNetCore.Mvc;

namespace ProjectWarehouse.Server.Models;

public class AppProblemDetails : ProblemDetails
{
    public Dictionary<string, AppFieldError[]> Errors { get; init; } = new();
}
