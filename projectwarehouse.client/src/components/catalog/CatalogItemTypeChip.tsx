import type {ChipProps} from "@mui/material";
import {Chip} from "@mui/material";
import type {CatalogItemType} from "@/api/types.gen";
import {CATALOG_ITEM_TYPE_CONFIG as TYPE_CONFIG} from "@/features/catalog";

interface CatalogItemTypeChipProps extends Omit<ChipProps, "label" | "color"> {
  type: CatalogItemType;
}

function CatalogItemTypeChip({type, ...props}: CatalogItemTypeChipProps) {
  const {label, color} = TYPE_CONFIG[type];
  return <Chip label={label} color={color} size="small" {...props} />;
}

export default CatalogItemTypeChip;
