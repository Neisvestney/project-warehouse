namespace ProjectWarehouse.Server.Infrastructure;

public static class Gtin
{
    /// <summary>
    /// Pads a GTIN-8/12/13/14 to the 14-digit form used by Честный знак. Null when the value is not digits of
    /// one of those lengths or its check digit is wrong.
    /// </summary>
    public static string? Normalize(string value)
    {
        var digits = value.Trim();
        if (digits.Length is not (8 or 12 or 13 or 14) || !digits.All(char.IsAsciiDigit)) return null;

        var gtin = digits.PadLeft(14, '0');
        return HasValidCheckDigit(gtin) ? gtin : null;
    }

    private static bool HasValidCheckDigit(string gtin14)
    {
        var sum = 0;
        for (var i = 0; i < 13; i++)
            sum += (gtin14[i] - '0') * (i % 2 == 0 ? 3 : 1);
        return (10 - sum % 10) % 10 == gtin14[13] - '0';
    }
}
