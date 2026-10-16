using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.DataProtection;

namespace ProjectWarehouse.Server.Infrastructure.Marketplaces;

public class MarketplaceCredentialProtector : IMarketplaceCredentialProtector
{
    private const string Purpose = "ProjectWarehouse.Marketplaces.ApiKey";

    private readonly IDataProtector _protector;
    private readonly ILogger<MarketplaceCredentialProtector> _logger;

    public MarketplaceCredentialProtector(IDataProtectionProvider provider,
        ILogger<MarketplaceCredentialProtector> logger)
    {
        _protector = provider.CreateProtector(Purpose);
        _logger = logger;
    }

    public string Protect(string plain) => _protector.Protect(plain);

    public string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);

    public bool TryUnprotect(string protectedValue, [NotNullWhen(true)] out string? plain)
    {
        try
        {
            plain = _protector.Unprotect(protectedValue);
            return true;
        }
        catch (Exception ex)
        {
            // key ring lost or rotated away — the account needs its key re-entered, this is not a 500
            _logger.LogWarning(ex, "Failed to decrypt a marketplace API key");
            plain = null;
            return false;
        }
    }

    public string Last4(string plain) => plain.Length <= 4 ? plain : plain[^4..];
}
