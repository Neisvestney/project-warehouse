namespace ProjectWarehouse.Server.Infrastructure.Files;

public enum FileFamily
{
    Unknown = 0,
    Jpeg = 1,
    Png = 2,
    Webp = 3,
    Gif = 4,
    Pdf = 5,
    Zip = 6,
    Text = 7,
}

/// <summary>
/// Content sniffing by leading bytes. Signatures identify a <em>family</em>, not a content type:
/// docx and xlsx are both zip archives and plain text has no signature at all, so the declared
/// type is accepted only when it is allow-listed and consistent with what the bytes look like.
/// </summary>
public static class FileSignatures
{
    /// <summary>Bytes needed to classify any known family.</summary>
    public const int HeaderLength = 12;

    public static FileFamily Detect(ReadOnlySpan<byte> head)
    {
        if (StartsWith(head, [0xFF, 0xD8, 0xFF])) return FileFamily.Jpeg;
        if (StartsWith(head, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])) return FileFamily.Png;
        if (StartsWith(head, "GIF8"u8)) return FileFamily.Gif;
        if (StartsWith(head, "%PDF-"u8)) return FileFamily.Pdf;
        if (StartsWith(head, "PK"u8)) return FileFamily.Zip;

        if (StartsWith(head, "RIFF"u8) && head.Length >= 12 && head[8..12].SequenceEqual("WEBP"u8))
            return FileFamily.Webp;

        // No signature: treat as text only if the head has no binary noise. A false negative here
        // is a rejected .txt, a false positive is a mislabelled binary — both end at the allow-list.
        return LooksLikeText(head) ? FileFamily.Text : FileFamily.Unknown;
    }

    public static bool IsConsistent(FileFamily family, string contentType) => family switch
    {
        FileFamily.Jpeg => contentType == "image/jpeg",
        FileFamily.Png => contentType == "image/png",
        FileFamily.Webp => contentType == "image/webp",
        FileFamily.Gif => contentType == "image/gif",
        FileFamily.Pdf => contentType == "application/pdf",
        FileFamily.Zip => contentType is "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            or "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        FileFamily.Text => contentType is "text/plain" or "text/csv",
        _ => false,
    };

    /// <summary>
    /// Extension for the storage key, derived from the validated content type rather than from the
    /// client-supplied name. Only for operational convenience — opening a file on disk directly.
    /// </summary>
    public static string ExtensionFor(string contentType) => contentType switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        "image/gif" => ".gif",
        "application/pdf" => ".pdf",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
        "text/plain" => ".txt",
        "text/csv" => ".csv",
        _ => ".bin",
    };

    private static bool StartsWith(ReadOnlySpan<byte> head, ReadOnlySpan<byte> prefix) =>
        head.Length >= prefix.Length && head[..prefix.Length].SequenceEqual(prefix);

    private static bool LooksLikeText(ReadOnlySpan<byte> head)
    {
        foreach (var b in head)
            if (b == 0 || (b < 0x20 && b != (byte)'\t' && b != (byte)'\n' && b != (byte)'\r'))
                return false;

        return true;
    }
}
