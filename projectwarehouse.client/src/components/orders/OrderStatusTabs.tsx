import {Box, Stack, Tab, Tabs} from "@mui/material";
import type {OrderStatus, OrderStatusCountDto} from "@/api/types.gen";
import {ORDER_STATUS_LABELS} from "./orderUtils";

interface OrderStatusTabsProps {
  value: OrderStatus | "";
  onChange: (value: OrderStatus | "") => void;
  statuses: OrderStatus[];
  counts?: OrderStatusCountDto[];
}

function CountBadge({count}: {count: number | undefined}) {
  if (count === undefined) return null;
  return (
    <Box
      component="span"
      sx={{
        px: 0.75,
        borderRadius: 1,
        typography: "caption",
        fontWeight: 600,
        lineHeight: "20px",
        bgcolor: "action.selected",
        color: count === 0 ? "text.disabled" : "text.primary",
      }}
    >
      {count.toLocaleString("ru-RU")}
    </Box>
  );
}

function OrderStatusTabs({value, onChange, statuses, counts}: OrderStatusTabsProps) {
  const countOf = (status: OrderStatus) => counts?.find((c) => c.status === status)?.count;
  const total = counts?.reduce((sum, c) => sum + c.count, 0);

  const label = (text: string, count: number | undefined) => (
    <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
      <span>{text}</span>
      <CountBadge count={count} />
    </Stack>
  );

  return (
    <Tabs
      value={value}
      onChange={(_, v: OrderStatus | "") => onChange(v)}
      variant="scrollable"
      scrollButtons="auto"
      allowScrollButtonsMobile
      sx={{borderBottom: 1, borderColor: "divider"}}
    >
      <Tab value="" label={label("Все", total)} />
      {statuses.map((s) => (
        <Tab key={s} value={s} label={label(ORDER_STATUS_LABELS[s], countOf(s))} />
      ))}
    </Tabs>
  );
}

export default OrderStatusTabs;
