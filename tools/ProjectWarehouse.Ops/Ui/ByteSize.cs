namespace ProjectWarehouse.Ops.Ui;

public static class ByteSize
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    /// Exact below a kilobyte: at that size the rounded form hides the difference between an empty
    /// archive and a nearly empty one.
    public static string Format(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";

        double value = bytes;
        var unit = 0;

        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.#} {Units[unit]}";
    }
}
