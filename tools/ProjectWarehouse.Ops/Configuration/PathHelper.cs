namespace ProjectWarehouse.Ops.Configuration;

public static class PathHelper
{
    public static string Expand(string path)
    {
        if (path.StartsWith('~'))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, path[1..].TrimStart('/', '\\'));
        }

        return path;
    }
}
