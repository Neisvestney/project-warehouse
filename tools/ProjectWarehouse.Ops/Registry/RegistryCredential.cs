namespace ProjectWarehouse.Ops.Registry;

public sealed record RegistryCredential(string Username, string Secret)
{
    /// Credential helpers answer with this username for token-based logins; the secret is then
    /// an identity token rather than a password.
    public bool IsIdentityToken => Username == "<token>";
}
