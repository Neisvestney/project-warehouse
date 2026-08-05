namespace ProjectWarehouse.Server.Integrations.Abstractions;

// Message is developer-facing English; Args carries what the UI needs to phrase it.
public record CredentialsValidationResult(bool IsValid, string? Message, IReadOnlyDictionary<string, object>? Args = null)
{
    public static CredentialsValidationResult Valid() => new(true, null);

    public static CredentialsValidationResult Invalid(string message, IReadOnlyDictionary<string, object>? args = null) =>
        new(false, message, args);
}
