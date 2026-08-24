import type {MouseEvent} from "react";
import {Chip, type ChipProps} from "@mui/material";
import {Link} from "react-router";
import {useHasPermission} from "@/hooks/usePermission";

interface WarehouseChipProps extends Omit<ChipProps, "label"> {
  warehouseId: string | null | undefined;
  name: string;
}

function WarehouseChip({warehouseId, name, ...chipProps}: WarehouseChipProps) {
  const canView = useHasPermission(["warehouses.view", "warehouses.view_assigned"]);

  if (!canView || !warehouseId) {
    return <Chip variant="outlined" size="small" label={name} {...chipProps} />;
  }

  return (
    <Chip
      component={Link}
      to={`/storage/warehouses/${warehouseId}`}
      variant="outlined"
      size="small"
      label={name}
      clickable
      onClick={(e: MouseEvent<HTMLElement>) => e.stopPropagation()}
      {...chipProps}
    />
  );
}

export default WarehouseChip;
