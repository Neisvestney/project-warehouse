import type {
  MarketplaceAccrualCategory,
  MarketplaceAccrualSource,
  PayoutsCategoryDto,
  PayoutsMoneyDto,
} from "@/api/types.gen";
import {formatMoney} from "@/components/analytics/analyticsFormat";

/** Display order: what comes in first, then what the marketplace keeps, the unmapped last. */
export const CATEGORY_LABELS: Record<MarketplaceAccrualCategory, string> = {
  sale: "Продажи",
  deliveryCharge: "Доставка от покупателя",
  compensation: "Компенсации",
  commission: "Комиссия",
  acquiring: "Эквайринг",
  logistics: "Логистика",
  returnLogistics: "Обратная логистика",
  processing: "Обработка",
  storage: "Хранение",
  advertising: "Реклама",
  services: "Доп. услуги",
  bonus: "Баллы и кэшбэк",
  penalty: "Штрафы",
  other: "Прочее",
  unknown: "Не распознано",
};

const ORDER = Object.keys(CATEGORY_LABELS) as MarketplaceAccrualCategory[];
const INCOME: MarketplaceAccrualCategory[] = ["sale", "deliveryCharge", "compensation"];

/** What the marketplace keeps: everything but sales, buyer delivery charges and compensations. */
export function isWithheld(category: MarketplaceAccrualCategory): boolean {
  return !INCOME.includes(category);
}

/** A category read from another document than the journal gets a line of its own. */
const SOURCE_LABELS: Partial<Record<MarketplaceAccrualSource, string>> = {
  buyoutReport: "Выкуп маркетплейсом",
};

export interface CategoryLine {
  /** Unique within one group: a category may come from several sources. */
  key: string;
  label: string;
  category: MarketplaceAccrualCategory;
  source: MarketplaceAccrualSource;
  amount: number;
  /** Of the sales; null without sales. */
  share: number | null;
}

export interface CategoriesSummary {
  sales: number;
  byPosting: CategoryLine[];
  byShop: CategoryLine[];
  /** Σ of the {@link isWithheld} categories; negative when the marketplace keeps money. */
  withheld: number;
  withheldShare: number | null;
  /** Σ of the lines tied to no posting. */
  byShopTotal: number;
  total: number;
}

function lines(
  categories: PayoutsCategoryDto[],
  byPosting: boolean,
  sales: number,
): CategoryLine[] {
  // a line from another source follows its category's journal line
  const rank = (c: PayoutsCategoryDto) =>
    ORDER.indexOf(c.category) * 2 + (c.source === "accrualJournal" ? 0 : 1);

  return categories
    .filter((c) => c.byPosting === byPosting && c.amount !== 0)
    .sort((a, b) => rank(a) - rank(b))
    .map((c) => ({
      key: `${c.category}-${c.source}`,
      label: SOURCE_LABELS[c.source] ?? CATEGORY_LABELS[c.category],
      category: c.category,
      source: c.source,
      amount: c.amount,
      share: sales > 0 ? c.amount / sales : null,
    }));
}

export function summarizeCategories(categories: PayoutsCategoryDto[]): CategoriesSummary {
  const sum = (items: PayoutsCategoryDto[]) => items.reduce((acc, c) => acc + c.amount, 0);
  const sales = sum(categories.filter((c) => c.category === "sale"));
  const withheld = sum(categories.filter((c) => isWithheld(c.category)));

  return {
    sales,
    byPosting: lines(categories, true, sales),
    byShop: lines(categories, false, sales),
    withheld,
    withheldShare: sales > 0 ? withheld / sales : null,
    byShopTotal: sum(categories.filter((c) => !c.byPosting)),
    total: sum(categories),
  };
}

export function categoryColor(category: MarketplaceAccrualCategory): string | undefined {
  if (category === "unknown") return "warning.main";
  return undefined;
}

/** The part of «Начислено» tied to no posting, as a caption; null when there is none. */
export function shopWideNote(money: PayoutsMoneyDto): string | null {
  const {byShopTotal} = summarizeCategories(money.categories);
  return byShopTotal === 0
    ? null
    : `в т.ч. по магазину ${formatMoney(byShopTotal, money.currencyCode)}`;
}
