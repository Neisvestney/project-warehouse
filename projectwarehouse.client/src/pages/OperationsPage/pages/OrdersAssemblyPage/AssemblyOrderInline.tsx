import {Box, Chip, Stack, Typography} from "@mui/material";
import {Link} from "react-router";
import type {AssemblyTaskDto, OrderDetailsDto} from "@/api/types.gen";
import OrderTypeChip from "@/components/orders/OrderTypeChip";
import {formatOrderNumber} from "@/components/orders/orderUtils";
import AssemblyTaskAccordion from "./AssemblyTaskAccordion";

interface AssemblyOrderInlineProps {
  order: OrderDetailsDto;
  task: AssemblyTaskDto;
  canFulfill: boolean;
  checked: boolean;
  onCheckChange: (checked: boolean) => void;
  batchEligible: boolean;
}

function AssemblyOrderInline({
  order,
  task,
  canFulfill,
  checked,
  onCheckChange,
  batchEligible,
}: AssemblyOrderInlineProps) {
  return (
    <Box>
      <Stack direction="row" sx={{alignItems: "center", gap: 1.5, px: 1, pb: 0.5}}>
        <Typography
          variant="subtitle2"
          component={Link}
          to={`/operations/orders/${order.id}`}
          sx={{
            textDecoration: "none",
            color: "inherit",
            "&:hover": {textDecoration: "underline"},
          }}
        >
          {formatOrderNumber(order.number)}
        </Typography>
        <OrderTypeChip type={order.type} />
        <Chip label={order.warehouseName} size="small" variant="outlined" />
      </Stack>
      <AssemblyTaskAccordion
        task={task}
        orderId={order.id}
        warehouseId={order.warehouseId}
        canFulfill={canFulfill}
        checked={checked}
        onCheckChange={onCheckChange}
        batchEligible={batchEligible}
        defaultExpanded
      />
    </Box>
  );
}

export default AssemblyOrderInline;
