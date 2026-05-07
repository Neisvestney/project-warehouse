using Microsoft.AspNetCore.Authorization;

namespace ProjectWarehouse.Server.Infrastructure;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
