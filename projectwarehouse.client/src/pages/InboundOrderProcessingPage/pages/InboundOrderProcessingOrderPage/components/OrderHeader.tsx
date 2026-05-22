import {Chip, Divider, Paper, Stack, Typography} from "@mui/material";
import {type InboundOrderProcessingDto} from "@/api/types.gen.ts";
import {INBOUND_ORDER_STATUS_COLORS, INBOUND_ORDER_STATUS_LABELS} from "@/utils/inboundOrderUtils";

interface OrderHeaderProps {
  order: InboundOrderProcessingDto;
}

function OrderHeader({order}: OrderHeaderProps) {
  const formattedDate = new Date(order.plannedStartDateTime).toLocaleString("ru-RU", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });

  return (
    <Paper sx={{px: 3, py: 2}}>
      <Stack spacing={1.5}>
        <Stack
          direction="row"
          spacing={1.5}
          sx={{alignItems: "center", flexWrap: "wrap"}}
          useFlexGap
        >
          <Typography variant="h6" sx={{fontWeight: 600}}>
            #{order.number}
          </Typography>
          {order.title && <Typography variant="h6">{order.title}</Typography>}
          <Chip
            label={INBOUND_ORDER_STATUS_LABELS[order.status]}
            color={INBOUND_ORDER_STATUS_COLORS[order.status]}
            size="small"
          />
        </Stack>
        <Stack
          direction="row"
          spacing={3}
          useFlexGap
          sx={{flexWrap: "wrap"}}
          divider={<Divider orientation="vertical" flexItem />}
        >
          <Stack spacing={0.25}>
            <Typography variant="caption" color="text.secondary">
              Склад
            </Typography>
            <Typography variant="body2" sx={{fontWeight: 500}}>
              {order.warehouse.name}
            </Typography>
          </Stack>
          <Stack spacing={0.25}>
            <Typography variant="caption" color="text.secondary">
              Дата начала
            </Typography>
            <Typography variant="body2" sx={{fontWeight: 500}}>
              {formattedDate}
            </Typography>
          </Stack>
          {order.notes && (
            <Stack spacing={0.25}>
              <Typography variant="caption" color="text.secondary">
                Примечания
              </Typography>
              <Typography variant="body2">{order.notes}</Typography>
            </Stack>
          )}
        </Stack>
      </Stack>
    </Paper>
  );
}

export default OrderHeader;
