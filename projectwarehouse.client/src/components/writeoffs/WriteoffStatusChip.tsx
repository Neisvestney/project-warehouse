import type {ChipProps} from "@mui/material";
import {Chip} from "@mui/material";
import type {WriteoffStatus} from "@/api/types.gen";
import {WRITEOFF_STATUS_LABELS} from "@/components/writeoffs/writeoffUtils";

const STATUS_COLORS: Record<WriteoffStatus, ChipProps["color"]> = {
  draft: "default",
  finished: "success",
  canceled: "error",
};

interface WriteoffStatusChipProps {
  status: WriteoffStatus;
}

function WriteoffStatusChip({status}: WriteoffStatusChipProps) {
  return <Chip label={WRITEOFF_STATUS_LABELS[status]} color={STATUS_COLORS[status]} size="small" />;
}

export default WriteoffStatusChip;
