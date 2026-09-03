using ProjectWarehouse.Ops.Configuration;
using ProjectWarehouse.Ops.Infrastructure;
using ProjectWarehouse.Ops.Infrastructure.Docker;
using ProjectWarehouse.Ops.Infrastructure.Git;
using ProjectWarehouse.Ops.Registry;

namespace ProjectWarehouse.Ops.Services;

public sealed record ServiceStatus(
    string Name,
    string TagVariable,
    string? DeployedTag,
    string? LatestTag,
    string? RegistryError);

public sealed record TargetStatus(
    string Name,
    string HostDescription,
    GitState? Git,
    string? RegistryValue,
    string? ExpectedRegistryValue,
    bool HasEnvFile,
    IReadOnlyList<ServiceStatus> Services,
    IReadOnlyList<ComposeService> Containers,
    string? EnvFileError);

public sealed class StatusService(OpsConfig config, RegistryClientFactory registries)
{
    public async Task<TargetStatus> ReadAsync(TargetContext target, CancellationToken cancellationToken)
    {
        var git = target.Config.GitPull
            ? await new GitClient(target).ReadStateAsync(cancellationToken)
            : null;

        var containers = await new ComposeClient(target).PsAsync(cancellationToken);
        var (env, envError) = await ReadEnvAsync(target, cancellationToken);

        var registry = target.Config.PullsFrom is { } registryName
            ? config.Registries[registryName]
            : null;

        var services = new List<ServiceStatus>();
        foreach (var serviceName in target.Config.Services)
        {
            var service = config.Services[serviceName];
            services.Add(await ReadServiceAsync(service, serviceName, env, registry, cancellationToken));
        }

        var registryVariable = target.Config.RegistryVariable;

        return new TargetStatus(
            target.Name,
            target.Host.Description,
            git,
            registryVariable is null ? null : env?.Get(registryVariable),
            registry?.ImagePrefix,
            target.EnvFilePath is not null,
            services,
            containers,
            envError);
    }

    private async Task<ServiceStatus> ReadServiceAsync(
        ServiceConfig service,
        string serviceName,
        EnvFile? env,
        RegistryConfig? registry,
        CancellationToken cancellationToken)
    {
        var deployed = env?.Get(service.TagVariable);

        if (registry is null)
            return new ServiceStatus(serviceName, service.TagVariable, deployed, null, null);

        try
        {
            var tags = await registries.Create(registry).ListTagsAsync(service.Image, cancellationToken);
            var latest = ImageVersion.Latest(tags);
            return new ServiceStatus(
                serviceName, service.TagVariable, deployed, latest?.ToString(), null);
        }
        catch (RegistryException ex)
        {
            return new ServiceStatus(serviceName, service.TagVariable, deployed, null, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return new ServiceStatus(serviceName, service.TagVariable, deployed, null, ex.Message);
        }
    }

    private static async Task<(EnvFile? Env, string? Error)> ReadEnvAsync(
        TargetContext target, CancellationToken cancellationToken)
    {
        if (target.EnvFilePath is not { } path)
            return (null, null);

        var content = await target.Host.ReadFileAsync(path, cancellationToken);
        return content is null ? (null, $"{path} not found") : (EnvFile.Parse(content), null);
    }
}
