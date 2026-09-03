namespace ProjectWarehouse.Ops.Registry;

/// A plain three-part release tag. Anything else in the repository — `latest`, a commit hash —
/// is not a version and takes no part in ordering or auto-increment.
public readonly record struct ImageVersion(int Major, int Minor, int Patch)
    : IComparable<ImageVersion>
{
    public static bool TryParse(string tag, out ImageVersion version)
    {
        version = default;

        var parts = tag.Split('.');
        if (parts.Length != 3)
            return false;

        if (!int.TryParse(parts[0], out var major)
            || !int.TryParse(parts[1], out var minor)
            || !int.TryParse(parts[2], out var patch))
        {
            return false;
        }

        if (major < 0 || minor < 0 || patch < 0)
            return false;

        version = new ImageVersion(major, minor, patch);
        return true;
    }

    public static ImageVersion? Latest(IEnumerable<string> tags)
    {
        ImageVersion? latest = null;

        foreach (var tag in tags)
        {
            if (TryParse(tag, out var version) && (latest is null || version.CompareTo(latest.Value) > 0))
                latest = version;
        }

        return latest;
    }

    public ImageVersion Bump(VersionBump bump) => bump switch
    {
        VersionBump.Major => new ImageVersion(Major + 1, 0, 0),
        VersionBump.Minor => new ImageVersion(Major, Minor + 1, 0),
        _ => new ImageVersion(Major, Minor, Patch + 1),
    };

    public int CompareTo(ImageVersion other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0)
            return major;

        var minor = Minor.CompareTo(other.Minor);
        return minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

public enum VersionBump
{
    Patch,
    Minor,
    Major,
}
