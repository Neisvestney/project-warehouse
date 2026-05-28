import type {ChipProps} from "@mui/material";
import {Chip} from "@mui/material";
import type {ReceiptStatus} from "@/api/types.gen";
import {RECEIPT_STATUS_LABELS} from "@/components/receipts/receiptUtils";

const STATUS_COLORS: Record<ReceiptStatus, ChipProps["color"]> = {
  draft: "default",
  planned: "info",
  processing: "warning",
  finished: "success",
  canceled: "error",
};

interface ReceiptStatusChipProps {
  status: ReceiptStatus;
}

function ReceiptStatusChip({status}: ReceiptStatusChipProps) {
  return <Chip label={RECEIPT_STATUS_LABELS[status]} color={STATUS_COLORS[status]} size="small" />;
}

export default ReceiptStatusChip;
