using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Models.Analytics;

namespace ProjectWarehouse.Server.Services;

/// <summary>A catalog item or a card by id, or an article by its normalized offer id.</summary>
public readonly record struct SubjectKey(Guid Id, string? Article) : IComparable<SubjectKey>
{
    public int CompareTo(SubjectKey other)
    {
        var byId = Id.CompareTo(other.Id);
        return byId != 0 ? byId : string.CompareOrdinal(Article, other.Article);
    }

    public override string ToString() => Article ?? Id.ToString();
}

/// <param name="Article">The offer id trimmed and upper-cased, as the article subject groups it.</param>
public sealed record CardInfo(Guid Id, Guid AccountId, string OfferId, string Article, string Name, string? ImageUrl);

/// <summary>
/// What a row of a per-item report is — a catalog item, a card or an article — and how it is shown. Rows
/// keyed by card id are folded into articles here, so every report groups articles the same way.
/// </summary>
public class AnalyticsSubjects(ApplicationDbContext db)
{
    public Task<Dictionary<Guid, CardInfo>> LoadCardsAsync(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var cardIds = ids.Where(id => id != null).Select(id => id!.Value).Distinct().ToList();
        return db.MarketplaceCards
            .Where(c => cardIds.Contains(c.Id))
            .Select(c => new CardInfo(c.Id, c.MarketplaceAccountId, c.OfferId, c.OfferId.Trim().ToUpper(),
                c.Name, c.PrimaryImageUrl))
            .ToDictionaryAsync(c => c.Id, ct);
    }

    /// <param name="id">A catalog item id for the item subject, a card id otherwise.</param>
    public static SubjectKey? KeyOf(AnalyticsAbcSubject subject, Guid? id, Dictionary<Guid, CardInfo> cards) =>
        id is not { } value ? null
        : subject == AnalyticsAbcSubject.Article ? new SubjectKey(Guid.Empty, cards[value].Article)
        : new SubjectKey(value, null);

    /// <param name="unitsByCard">Picks the card whose name and image an article shows: the one with the most units.</param>
    public async Task<Dictionary<SubjectKey, AbcSubjectDto>> DescribeAsync(
        AnalyticsAbcSubject subject,
        IReadOnlyCollection<SubjectKey> keys,
        Dictionary<Guid, CardInfo> cards,
        Dictionary<Guid, int> unitsByCard,
        List<AnalyticsAccount> accounts,
        CancellationToken ct)
    {
        if (subject == AnalyticsAbcSubject.CatalogItem)
        {
            var ids = keys.Select(k => k.Id).ToList();
            var names = await db.CatalogItems
                .Where(c => ids.Contains(c.Id))
                .Select(c => new { c.Id, c.FullName, c.Type })
                .ToDictionaryAsync(c => c.Id, ct);
            return keys.ToDictionary(k => k, k => new AbcSubjectDto
            {
                Key = k.ToString(),
                Name = names[k.Id].FullName,
                CatalogItemId = k.Id,
                Type = names[k.Id].Type,
            });
        }

        var accountsById = accounts.ToDictionary(a => a.Id);
        AbcSubjectAccountDto Account(Guid id) => new()
        {
            Id = id, Name = accountsById[id].Name, Type = accountsById[id].Type,
        };

        if (subject == AnalyticsAbcSubject.Card)
            return keys.ToDictionary(k => k, k =>
            {
                var card = cards[k.Id];
                return new AbcSubjectDto
                {
                    Key = k.ToString(),
                    Name = card.Name,
                    MarketplaceCardId = card.Id,
                    OfferId = card.OfferId,
                    ImageUrl = card.ImageUrl,
                    Accounts = [Account(card.AccountId)],
                };
            });

        var cardsByArticle = cards.Values
            .Where(c => unitsByCard.ContainsKey(c.Id))
            .ToLookup(c => c.Article);

        return keys.ToDictionary(k => k, k =>
        {
            var articleCards = cardsByArticle[k.Article!].ToList();
            var best = articleCards.OrderByDescending(c => unitsByCard[c.Id]).ThenBy(c => c.Id).First();
            return new AbcSubjectDto
            {
                Key = k.ToString(),
                Name = best.Name,
                OfferId = best.OfferId,
                ImageUrl = best.ImageUrl,
                Accounts = articleCards
                    .Select(c => c.AccountId)
                    .Distinct()
                    .Select(Account)
                    .OrderBy(a => a.Name, StringComparer.CurrentCulture)
                    .ToList(),
            };
        });
    }
}
