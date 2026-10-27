import type {MarketplaceSupplyDto} from "@/api/types.gen";

export function formatSupplyDate(supply: MarketplaceSupplyDto): string {
  const date = new Date(supply.createdAt);
  return `${date.toLocaleDateString("ru-RU")} ${date.toLocaleTimeString("ru-RU", {hour: "2-digit", minute: "2-digit"})}`;
}

export function supplyName(supply: MarketplaceSupplyDto): string {
  return supply.name ?? supply.externalId;
}
