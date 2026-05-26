import type {ChipProps} from "@mui/material";
import {Chip} from "@mui/material";
import type {CatalogItemType} from "@/api/types.gen";

const TYPE_CONFIG: Record<CatalogItemType, {label: string; color: ChipProps["color"]}> = {
  standard: {label: "Товар", color: "default"},
  unit: {label: "Единица", color: "info"},
  productGroup: {label: "Группа", color: "secondary"},
  variation: {label: "Вариация", color: "warning"},
  bundle: {label: "Комплект", color: "success"},
  assembledBundle: {label: "Сборка", color: "primary"},
};

interface CatalogItemTypeChipProps extends Omit<ChipProps, "label" | "color"> {
  type: CatalogItemType;
}

function CatalogItemTypeChip({type, ...props}: CatalogItemTypeChipProps) {
  const {label, color} = TYPE_CONFIG[type];
  return <Chip label={label} color={color} size="small" {...props} />;
}

export default CatalogItemTypeChip;
