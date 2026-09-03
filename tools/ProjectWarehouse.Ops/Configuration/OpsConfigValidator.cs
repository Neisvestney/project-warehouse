namespace ProjectWarehouse.Ops.Configuration;

public static class OpsConfigValidator
{
    public static IReadOnlyList<string> Validate(OpsConfig config)
    {
        var errors = new List<string>();

        if (config.Targets.Count == 0)
            errors.Add("No targets defined.");

        foreach (var (name, registry) in config.Registries)
            ValidateRegistry(name, registry, errors);

        foreach (var (name, service) in config.Services)
            ValidateService(name, service, errors);

        foreach (var (name, target) in config.Targets)
            ValidateTarget(name, target, config, errors);

        return errors;
    }

    private static void ValidateRegistry(string name, RegistryConfig registry, List<string> errors)
    {
        var scope = $"registries.{name}";

        if (string.IsNullOrWhiteSpace(registry.Url))
            errors.Add($"{scope}: url is required.");
        else if (!Uri.TryCreate(registry.Url, UriKind.Absolute, out _))
            errors.Add($"{scope}: url must be an absolute URI, got '{registry.Url}'.");

        if (string.IsNullOrWhiteSpace(registry.Project))
            errors.Add($"{scope}: project is required.");

        if (registry.Credentials == RegistryCredentials.Inline
            && (string.IsNullOrWhiteSpace(registry.Username) || string.IsNullOrWhiteSpace(registry.Password)))
        {
            errors.Add($"{scope}: credentials 'inline' requires both username and password.");
        }
    }

    private static void ValidateService(string name, ServiceConfig service, List<string> errors)
    {
        var scope = $"services.{name}";

        if (string.IsNullOrWhiteSpace(service.Dockerfile))
            errors.Add($"{scope}: dockerfile is required.");

        if (string.IsNullOrWhiteSpace(service.Image))
            errors.Add($"{scope}: image is required.");

        if (string.IsNullOrWhiteSpace(service.ComposeService))
            errors.Add($"{scope}: composeService is required.");

        if (string.IsNullOrWhiteSpace(service.TagVariable))
            errors.Add($"{scope}: tagVariable is required.");
    }

    private static void ValidateTarget(string name, TargetConfig target, OpsConfig config, List<string> errors)
    {
        var scope = $"targets.{name}";

        if (string.IsNullOrWhiteSpace(target.ComposeFile))
            errors.Add($"{scope}: composeFile is required.");

        if (target.Kind == TargetKind.Ssh)
        {
            if (target.Ssh is null)
                errors.Add($"{scope}: ssh is required when kind is 'ssh'.");
            else
            {
                if (string.IsNullOrWhiteSpace(target.Ssh.Host))
                    errors.Add($"{scope}.ssh: host is required.");
                if (string.IsNullOrWhiteSpace(target.Ssh.User))
                    errors.Add($"{scope}.ssh: user is required.");
            }
        }

        if (target.GitPull && string.IsNullOrWhiteSpace(target.RepoDir))
            errors.Add($"{scope}: repoDir is required when gitPull is true.");

        if (!string.IsNullOrWhiteSpace(target.PullsFrom))
        {
            if (!config.Registries.ContainsKey(target.PullsFrom))
                errors.Add($"{scope}: pullsFrom '{target.PullsFrom}' is not a known registry.");

            if (string.IsNullOrWhiteSpace(target.EnvFile))
                errors.Add($"{scope}: envFile is required when pullsFrom is set.");

            if (string.IsNullOrWhiteSpace(target.RegistryVariable))
                errors.Add($"{scope}: registryVariable is required when pullsFrom is set.");
        }

        if (target.Services.Count == 0)
            errors.Add($"{scope}: services must list at least one service.");

        var tagVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var serviceName in target.Services)
        {
            if (!config.Services.TryGetValue(serviceName, out var service))
            {
                errors.Add($"{scope}: service '{serviceName}' is not defined.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(service.TagVariable))
                continue;

            if (tagVariables.TryGetValue(service.TagVariable, out var owner))
            {
                errors.Add(
                    $"{scope}: services '{owner}' and '{serviceName}' share tagVariable "
                        + $"'{service.TagVariable}' — they would overwrite each other in the env file.");
            }
            else
            {
                tagVariables[service.TagVariable] = serviceName;
            }
        }

        if (target.RegistryVariable is { } registryVariable
            && tagVariables.ContainsKey(registryVariable))
        {
            errors.Add($"{scope}: registryVariable '{registryVariable}' collides with a service tagVariable.");
        }

        if (target.Postgres is { } postgres && string.IsNullOrWhiteSpace(postgres.Database))
            errors.Add($"{scope}.postgres: database is required.");
    }
}
