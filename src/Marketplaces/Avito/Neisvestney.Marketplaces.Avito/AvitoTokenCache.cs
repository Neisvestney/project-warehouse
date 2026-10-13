using System.Collections.Concurrent;

namespace Neisvestney.Marketplaces.Avito;

/// <summary>
/// Access tokens per <see cref="AvitoCredentials.ClientId"/>, shared by every <see cref="AvitoAuthHandler"/>.
/// Register it as a singleton: <c>IHttpClientFactory</c> recycles handlers every few minutes, and a cache
/// owned by the handler would request a new token each time.
/// </summary>
public sealed class AvitoTokenCache(TimeProvider? timeProvider = null)
{
    private static readonly TimeSpan MaxRefreshMargin = TimeSpan.FromMinutes(5);

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, Entry> _tokens = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);

    internal async Task<string> GetAsync(
        string clientId, Func<CancellationToken, Task<(string Token, TimeSpan Lifetime)>> request, CancellationToken cancellationToken)
    {
        if (TryGetValid(clientId, out var token))
            return token;

        // one token request per account at a time; the others wait and reuse its result
        var gate = _gates.GetOrAdd(clientId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (TryGetValid(clientId, out token))
                return token;

            var (issued, lifetime) = await request(cancellationToken);
            // refresh early so a request in flight never carries a token that expires on the way
            var margin = TimeSpan.FromTicks(Math.Min(MaxRefreshMargin.Ticks, lifetime.Ticks / 10));
            _tokens[clientId] = new Entry(issued, _time.GetUtcNow() + lifetime - margin);
            return issued;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Drops <paramref name="token"/> unless another request has already replaced it.</summary>
    internal void Invalidate(string clientId, string token)
    {
        if (_tokens.TryGetValue(clientId, out var entry) && entry.Token == token)
            _tokens.TryRemove(KeyValuePair.Create(clientId, entry));
    }

    private bool TryGetValid(string clientId, out string token)
    {
        if (_tokens.TryGetValue(clientId, out var entry) && _time.GetUtcNow() < entry.RefreshAt)
        {
            token = entry.Token;
            return true;
        }
        token = "";
        return false;
    }

    private sealed record Entry(string Token, DateTimeOffset RefreshAt)
    {
        private bool PrintMembers(System.Text.StringBuilder builder) => false;
    }
}
