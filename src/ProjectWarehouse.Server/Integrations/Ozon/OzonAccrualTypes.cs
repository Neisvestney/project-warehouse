using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Integrations.Ozon;

/// <summary>
/// Ozon accrual <c>type_id</c>s, as /v1/finance/accrual/types lists them, collapsed into categories. A type
/// Ozon adds later lands as <see cref="MarketplaceAccrualCategory.Unknown"/> with its raw id kept, and is
/// logged; re-importing the period after mapping it here reclassifies the stored rows.
/// </summary>
public static class OzonAccrualTypes
{
    /// <summary>SaleCommission — the one type the finance API splits into the sale and its commission.</summary>
    public const int SaleCommission = 69;

    private static readonly Dictionary<int, MarketplaceAccrualCategory> Categories = Build(
        // rFBS agent fees are the marketplace's cut of a sale the seller delivers
        (MarketplaceAccrualCategory.Commission, [63, 66]),
        (MarketplaceAccrualCategory.Logistics,
            [12, 13, 28, 29, 30, 32, 43, 44, 56, 58, 64, 67, 73, 88, 98, 99, 100, 110, 111, 112, 114, 120, 121]),
        (MarketplaceAccrualCategory.DeliveryCharge, [62, 124]),
        (MarketplaceAccrualCategory.ReturnLogistics, [2, 6, 9, 40, 45, 53, 59, 65, 71, 113, 115]),
        (MarketplaceAccrualCategory.Processing,
            [16, 17, 21, 34, 38, 39, 42, 77, 82, 84, 85, 86, 97, 101, 106, 107, 108, 109]),
        (MarketplaceAccrualCategory.Acquiring, [1]),
        (MarketplaceAccrualCategory.Advertising,
            [3, 4, 5, 19, 23, 27, 31, 33, 36, 41, 47, 49, 50, 54, 55, 61, 70, 74, 75, 80, 87, 96, 116, 118, 119, 130]),
        (MarketplaceAccrualCategory.Storage, [46, 60, 78, 79, 102]),
        (MarketplaceAccrualCategory.Penalty, [14, 89, 90, 91, 92, 93, 94]),
        (MarketplaceAccrualCategory.Compensation, [10, 25, 104]),
        (MarketplaceAccrualCategory.Bonus, [48]),
        (MarketplaceAccrualCategory.Services,
            [18, 20, 22, 24, 26, 35, 37, 51, 52, 68, 76, 95, 105, 117, 122, 123, 125, 126, 127, 128]),
        (MarketplaceAccrualCategory.Other, [7, 8, 11, 15, 57, 72, 81, 83, 103, 129]));

    public static MarketplaceAccrualCategory CategoryOf(int typeId) =>
        Categories.GetValueOrDefault(typeId, MarketplaceAccrualCategory.Unknown);

    private static Dictionary<int, MarketplaceAccrualCategory> Build(
        params (MarketplaceAccrualCategory Category, int[] TypeIds)[] groups) =>
        groups
            .SelectMany(g => g.TypeIds.Select(id => (Id: id, g.Category)))
            .ToDictionary(x => x.Id, x => x.Category);
}
