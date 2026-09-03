using System.Text.RegularExpressions;

namespace ProjectWarehouse.Ops.Configuration;

/// Path values may carry {currentConfigDir} and {projectDir}. Expansion happens per file while
/// loading, so {currentConfigDir} always means the file the value was written in — not whichever
/// file ended up including it.
public static partial class PathTemplate
{
    public static string Expand(string value, string currentConfigDir, string projectDir)
    {
        if (!value.Contains('{'))
            return value;

        var expanded = value
            .Replace("{currentConfigDir}", currentConfigDir, StringComparison.OrdinalIgnoreCase)
            .Replace("{projectDir}", projectDir, StringComparison.OrdinalIgnoreCase);

        var leftover = TokenPattern().Match(expanded);
        if (leftover.Success)
        {
            throw new OpsConfigException(
                $"Unknown path variable '{leftover.Value}' in '{value}'. "
                    + "Known variables: {currentConfigDir}, {projectDir}.");
        }

        return expanded;
    }

    [GeneratedRegex(@"\{[^}]*\}")]
    private static partial Regex TokenPattern();
}
