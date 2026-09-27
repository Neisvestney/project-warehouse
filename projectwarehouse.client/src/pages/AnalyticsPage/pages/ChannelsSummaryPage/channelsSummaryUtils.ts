import type {ChannelSummaryRowDto} from "@/api/types.gen";

/** What it takes to name and paint a channel: a summary row or any row shaped like one. */
export type ChannelRef = Pick<
  ChannelSummaryRowDto,
  "kind" | "marketplaceAccountId" | "marketplaceType" | "name"
>;

const numberFormat = new Intl.NumberFormat("ru-RU");
const moneyFormat = new Intl.NumberFormat("ru-RU", {maximumFractionDigits: 0});
const averageFormat = new Intl.NumberFormat("ru-RU", {maximumFractionDigits: 1});
const percentFormat = new Intl.NumberFormat("ru-RU", {style: "percent", maximumFractionDigits: 1});

export function formatNumber(value: number): string {
  return numberFormat.format(value);
}

export function formatAverage(value: number): string {
  return averageFormat.format(value);
}

export function formatMoney(amount: number, currencyCode: string): string {
  return `${moneyFormat.format(amount)} ${currencyCode}`;
}

export function formatPercent(value: number | null | undefined): string {
  return value == null ? "—" : percentFormat.format(value);
}

export function channelLabel(row: ChannelRef): string {
  switch (row.kind) {
    case "direct":
      return "Прямые · все";
    case "directUntagged":
      return "Без тегов";
    default:
      return row.name ?? "";
  }
}
