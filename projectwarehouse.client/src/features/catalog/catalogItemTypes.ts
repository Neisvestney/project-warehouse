import type {ChipProps} from "@mui/material";
import type {CatalogItemType} from "@/api/types.gen";

export type CatalogItemTypeConfig = {
  label: string;
  color: ChipProps["color"];
};

export const CATALOG_ITEM_TYPE_CONFIG: Record<CatalogItemType, CatalogItemTypeConfig> = {
  standard: {label: "Товар", color: "default"},
  unit: {label: "Штучный", color: "info"},
  productGroup: {label: "Группа", color: "secondary"},
  variation: {label: "Вариация", color: "warning"},
  bundle: {label: "Комплект", color: "success"},
};

export const CATALOG_ITEM_TYPES = Object.keys(CATALOG_ITEM_TYPE_CONFIG) as CatalogItemType[];

/** Types that physically hold stock — groups, variations and bundles never have inventory of their own. */
export const PHYSICAL_CATALOG_ITEMS: CatalogItemType[] = ["standard", "unit"];
