import {Chip, Tooltip} from "@mui/material";
import {WAREHOUSE_STATUS_LABELS} from "../marketplaceUtils";
import type {MarketplaceWarehouseStatus} from "@/api/types.gen";

interface WarehouseStatusChipProps {
  status: MarketplaceWarehouseStatus;
  /** Формулировка площадки — только для разбора, почему склад в этом состоянии. */
  externalStatus?: string | null;
}

function WarehouseStatusChip({status, externalStatus}: WarehouseStatusChipProps) {
  const {label, color} = WAREHOUSE_STATUS_LABELS[status];
  const chip = <Chip label={label} color={color} size="small" />;

  if (!externalStatus) return chip;
  return <Tooltip title={`Статус площадки: ${externalStatus}`}>{chip}</Tooltip>;
}

export default WarehouseStatusChip;
