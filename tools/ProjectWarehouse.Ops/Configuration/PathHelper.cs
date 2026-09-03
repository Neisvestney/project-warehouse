namespace ProjectWarehouse.Ops.Configuration;

public static class PathHelper
{
    public static string Expand(string path)
    {
        if (path.StartsWith('~'))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // Normalized: a `~/.ssh/key` written the POSIX way would otherwise come back with both
            // separators in it and show up that way in every message.
            return Path.GetFullPath(Path.Combine(home, path[1..].TrimStart('/', '\\')));
        }

        return path;
    }
}
