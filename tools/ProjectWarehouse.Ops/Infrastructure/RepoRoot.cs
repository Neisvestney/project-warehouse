namespace ProjectWarehouse.Ops.Infrastructure;

public static class RepoRoot
{
    /// Walks up from the working directory looking for .git — pwops is usually launched from
    /// tools/ProjectWarehouse.Ops, while every configured path is relative to the repo root.
    public static string Find()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git"))
                || File.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
