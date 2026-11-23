using System.IO.Compression;
using System.Runtime.CompilerServices;
using Neisvestney.Marketplaces.YandexMarket;
using ProjectWarehouse.Server.Integrations.Abstractions;

namespace ProjectWarehouse.Server.Integrations.YandexMarket;

public sealed partial class YandexMarketClient
{
    public async IAsyncEnumerable<IReadOnlyList<ExternalAccrual>> GetAccrualsAsync(DateOnly from, DateOnly to,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        if (to > today) to = today;
        for (var start = from; start <= to;)
        {
            var end = start.AddMonths(3).AddDays(-1);
            if (end > to) end = to;
            await quota.WaitAsync(BusinessId, ct);
            GenerateReportResponse generated;
            try
            {
                generated = await api.GenerateUnitedNettingReportAsync(ReportFormatType.JSON, ReportLanguageType.RU,
                    new GenerateUnitedNettingReportRequest
                    {
                        BusinessId = BusinessId, CampaignIds = [CampaignId],
                        DateFrom = Utc(start.ToDateTime(TimeOnly.MinValue)),
                        DateTo = Utc(end.ToDateTime(TimeOnly.MinValue)),
                    }, ct);
            }
            catch (YandexMarketApiException ex) when (ex.StatusCode == 420)
            {
                quota.Refused(BusinessId);
                throw;
            }
            var reportId = generated.Result?.ReportId
                ?? throw new InvalidDataException("Yandex Market returned no report id.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(_options.ReportTimeoutSeconds));
            string? file = null;
            while (file is null)
            {
                var info = (await api.GetReportInfoAsync(reportId, null, timeout.Token)).Result
                    ?? throw new InvalidDataException("Yandex Market returned no report information.");
                if (info.Status == ReportStatusType.FAILED)
                    throw new InvalidDataException($"Yandex Market report failed: {info.SubStatus}.");
                if (info.Status == ReportStatusType.DONE)
                    file = info.File ?? throw new InvalidDataException("Yandex Market completed a report without a file.");
                else
                    await Task.Delay(_options.ReportPollDelayMs, timeout.Token);
            }
            if (!Uri.TryCreate(file, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
                || !string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort)
                throw new InvalidDataException("Yandex Market returned an invalid report URL.");
            using var response = await httpClients.CreateClient("yandex-market-reports")
                .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            response.EnsureSuccessStatusCode();
            var bytes = await ReadBoundedAsync(response.Content, timeout.Token);
            using var archive = new ZipArchive(new MemoryStream(bytes));
            var entries = archive.Entries.Where(e => e.Name.Equals("transaction_date.json", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (entries.Length != 1 || entries[0].Length > _options.MaxDownloadBytes)
                throw new InvalidDataException("Yandex Market report must contain one bounded transaction_date.json sheet.");
            await using var sheet = entries[0].Open();
            var parsed = await YandexMarketAccrualParser.ParseAsync(sheet, _options.MaxDownloadBytes, timeout.Token);
            yield return parsed;
            start = end.AddDays(1);
        }
    }
}
