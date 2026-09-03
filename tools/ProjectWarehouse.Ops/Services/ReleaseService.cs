using ProjectWarehouse.Ops.Configuration;
using ProjectWarehouse.Ops.Infrastructure.Docker;
using ProjectWarehouse.Ops.Registry;

namespace ProjectWarehouse.Ops.Services;

public sealed record ReleaseCandidate(
    string ServiceName,
    ServiceConfig Service,
    ImageVersion? Current,
    string? RegistryError);

public sealed record ReleaseItem(string ServiceName, ServiceConfig Service, ImageVersion Version)
{
    public string Reference(RegistryConfig registry) =>
        $"{registry.ImagePrefix}/{Service.Image}:{Version}";
}

public sealed class ReleaseService(RegistryConfig registry, IRegistryClient client, DockerCli docker)
{
    /// The first release of an image starts here — nothing in the registry parses as a version.
    private static readonly ImageVersion FirstVersion = new(0, 0, 1);

    public async Task<IReadOnlyList<ReleaseCandidate>> SurveyAsync(
        IEnumerable<KeyValuePair<string, ServiceConfig>> services,
        CancellationToken cancellationToken)
    {
        var candidates = new List<ReleaseCandidate>();

        foreach (var (name, service) in services)
        {
            try
            {
                var tags = await client.ListTagsAsync(service.Image, cancellationToken);
                candidates.Add(new ReleaseCandidate(name, service, ImageVersion.Latest(tags), null));
            }
            catch (Exception ex) when (ex is RegistryException or HttpRequestException)
            {
                candidates.Add(new ReleaseCandidate(name, service, null, ex.Message));
            }
        }

        return candidates;
    }

    public static ImageVersion Next(ReleaseCandidate candidate, VersionBump bump) =>
        candidate.Current?.Bump(bump) ?? FirstVersion;

    public async Task BuildAsync(
        ReleaseItem item, Action<string> onLine, CancellationToken cancellationToken)
    {
        var buildArgs = new Dictionary<string, string>(item.Service.BuildArgs, StringComparer.Ordinal);

        if (item.Service.VersionBuildArg is { } versionArg)
            buildArgs[versionArg] = item.Version.ToString();

        var result = await docker.BuildAsync(
            item.Service.Dockerfile,
            item.Service.Context,
            [item.Reference(registry)],
            buildArgs,
            onLine,
            cancellationToken);

        if (!result.Succeeded)
            throw new ReleaseException($"docker build failed for {item.ServiceName}.", result.StdOut);
    }

    public async Task PushAsync(ReleaseItem item, CancellationToken cancellationToken)
    {
        var reference = item.Reference(registry);
        var exitCode = await docker.PushAsync(reference, cancellationToken);

        if (exitCode != 0)
            throw new ReleaseException($"docker push failed for {reference}.", string.Empty);
    }
}

public sealed class ReleaseException(string message, string log) : Exception(message)
{
    public string Log => log;
}
