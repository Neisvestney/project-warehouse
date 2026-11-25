using Neisvestney.Marketplaces.YandexMarket;
using Microsoft.AspNetCore.WebUtilities;
using ProjectWarehouse.Server.Infrastructure.Labels;
using ProjectWarehouse.Server.Infrastructure.Marketplaces;
using ProjectWarehouse.Server.Integrations.Abstractions;

namespace ProjectWarehouse.Server.Integrations.YandexMarket;

public sealed partial class YandexMarketClient
{
    public async Task<ExternalLabelDocument> GetLabelsAsync(IReadOnlyList<string> ids, CancellationToken ct)
    {
        if (IsFby)
            return new(false, ids, null, null, ids.Select(id => new ExternalLabelFailure(id, null)).ToArray());
        var pages = new List<ExternalLabelPage>();
        var unprinted = new List<ExternalLabelFailure>();
        var unreadable = new List<ExternalLabelFailure>();
        foreach (var id in ids)
        {
            try
            {
                var labels = (await api.GetOrderLabelsDataAsync(CampaignId, ParseId(id), ct)).Result;
                if (labels is null || labels.ParcelBoxLabels.Count == 0)
                {
                    unprinted.Add(new(id, null));
                    continue;
                }
                foreach (var label in labels.ParcelBoxLabels)
                {
                    if (label.OrderId != ParseId(id))
                        throw new InvalidOperationException("Yandex Market returned a label for a different order.");
                    var url = TrustedApiUrl(label.Url);
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Add("Api-Key", Credentials.ApiKey);
                    using var response = await httpClients.CreateClient("yandex-market-labels").SendAsync(request,
                        HttpCompletionOption.ResponseHeadersRead, ct);
                    if (response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Conflict)
                    {
                        unprinted.Add(new(id, null));
                        break;
                    }
                    response.EnsureSuccessStatusCode();
                    var bytes = await ReadBoundedAsync(response.Content, ct);
                    try
                    {
                        if (LabelPdfComposer.PageCount(bytes) != 1)
                            throw new InvalidDataException("Yandex Market parcel label must contain one page.");
                        pages.Add(new(id, LabelPdfComposer.FromPdf(bytes, 58, 40, _options.LabelScale), LabelKind.YandexMarketLabel,
                            label.FulfilmentId, Id(label.BoxId)));
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        unreadable.Add(new(id, "The parcel label is not a readable single-page PDF."));
                        break;
                    }
                }
            }
            catch (YandexMarketApiException ex) when (ex.StatusCode is 404 or 409)
            {
                unprinted.Add(new(id, null));
            }
        }
        return new(true, ids, null, null, unprinted, pages, unreadable);
    }

    private static Uri TrustedApiUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.Equals(uri.Host, "api.partner.market.yandex.ru", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Yandex Market returned an untrusted label URL.");
        var query = QueryHelpers.ParseQuery(uri.Query);
        query.Remove("format");
        var parameters = query.SelectMany(p => p.Value.Select(v => new KeyValuePair<string, string?>(p.Key, v)))
            .Append(new("format", "A9_HORIZONTALLY"));
        return new Uri(QueryHelpers.AddQueryString(uri.GetLeftPart(UriPartial.Path), parameters));
    }

    private async Task<byte[]> ReadBoundedAsync(HttpContent content, CancellationToken ct)
    {
        if (content.Headers.ContentLength > _options.MaxDownloadBytes)
            throw new InvalidDataException("Yandex Market download exceeds the configured size limit.");
        await using var input = await content.ReadAsStreamAsync(ct);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer, ct)) != 0)
        {
            if (output.Length + read > _options.MaxDownloadBytes)
                throw new InvalidDataException("Yandex Market download exceeds the configured size limit.");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }
}
