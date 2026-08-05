using System.Diagnostics.CodeAnalysis;

namespace ProjectWarehouse.Server.Infrastructure.Marketplaces;

/// <summary>
/// Encrypts marketplace API keys. Deliberately a service rather than an EF ValueConverter: a converter
/// would decrypt on every entity read, including listings, putting the plaintext key in memory where it
/// is never needed. This keeps a single point where the key becomes readable — the call to a provider.
/// </summary>
public interface IMarketplaceCredentialProtector
{
    string Protect(string plain);

    /// <summary>Throws when the key ring can no longer read the ciphertext. Prefer <see cref="TryUnprotect"/>.</summary>
    string Unprotect(string protectedValue);

    bool TryUnprotect(string protectedValue, [NotNullWhen(true)] out string? plain);

    /// <summary>Trailing characters kept alongside the ciphertext so the UI can render a mask.</summary>
    string Last4(string plain);
}
