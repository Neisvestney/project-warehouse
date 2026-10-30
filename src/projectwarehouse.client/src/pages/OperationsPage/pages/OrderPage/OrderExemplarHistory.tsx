import {useState} from "react";
import {Alert, Box, Chip, Link, MenuItem, Paper, Stack, TextField, Typography} from "@mui/material";
import {Link as RouterLink} from "react-router";
import type {OrderExemplarHistoryDto, OrderFinanceAccrualDto} from "@/api/types.gen";
import {
  MARKETPLACE_ORDER_STATUS_LABELS,
  MARKETPLACE_RETURN_KIND_LABELS,
} from "@/components/orders/marketplace/marketplaceOrderUtils";
import {formatOrderNumber, ORDER_TYPE_LABELS} from "@/components/orders/orderUtils";
import {formatDateOnly} from "@/utils/dateOnly";
import FinanceAccrualsSection from "./FinanceAccrualsSection";

interface HistoryEvent {
  key: string;
  day: string | null;
  label: string;
  orderId?: string | null;
  detail?: string | null;
}

const moscowDay = new Intl.DateTimeFormat("sv-SE", {timeZone: "Europe/Moscow"});
const dayOf = (value: string | null | undefined) =>
  value ? moscowDay.format(new Date(value)) : null;

function eventsFor(history: OrderExemplarHistoryDto): HistoryEvent[] {
  const events: HistoryEvent[] = [];
  for (const order of history.orders) {
    const steps: [string, string | null | undefined][] = [
      ["Оформлен заказ", order.inProcessAt],
      ["Передан в доставку", order.deliveringAt],
      ["Доставлен", order.deliveredAt],
      ["Отменён", order.cancelledAt],
    ];
    for (const [label, date] of steps) {
      if (date)
        events.push({key: `${order.id}-${label}`, day: dayOf(date), label, orderId: order.id});
    }
    if (!steps.some(([, date]) => date)) {
      events.push({key: order.id, day: null, label: "Связанный заказ", orderId: order.id});
    }
  }
  const groups = new Map<string, OrderFinanceAccrualDto[]>();
  for (const row of history.accruals) {
    const group = groups.get(row.externalId) ?? [];
    group.push(row);
    groups.set(row.externalId, group);
  }
  for (const [id, rows] of groups) {
    const first = rows[0];
    const sale = rows.find((row) => row.category === "sale");
    events.push({
      key: `accrual-${id}`,
      day: dayOf(first.occurredAt) ?? first.date,
      label: sale
        ? sale.amount < 0
          ? "Сторно продажи"
          : "Продажа по отчёту"
        : "Финансовая операция",
      orderId: first.orderId,
      detail: `${first.rawTypeId || "Начисление"} · № ${id} · учётный день ${formatDateOnly(first.date)}`,
    });
  }
  for (const returned of history.returns) {
    const buyerReturn = returned.kind === "customerReturn" || returned.kind === "partialRefusal";
    events.push({
      key: returned.id,
      day: returned.requestedOn ?? (returned.saleReversedOn || dayOf(returned.returnedAt)),
      label: buyerReturn
        ? MARKETPLACE_RETURN_KIND_LABELS[returned.kind]
        : "Попытка возврата продавцу",
      orderId: returned.orderId,
      detail: [
        returned.statusName,
        returned.reason,
        returned.isCancelled ? "Попытка отменена" : null,
      ]
        .filter(Boolean)
        .join(" · "),
    });
    if (!buyerReturn && returned.returnedAt)
      events.push({
        key: `${returned.id}-ready`,
        day: dayOf(returned.returnedAt),
        label: "Готовность возврата продавцу",
        orderId: returned.orderId,
      });
    if (returned.finalAt)
      events.push({
        key: `${returned.id}-final`,
        day: dayOf(returned.finalAt),
        label: buyerReturn ? "Завершение возврата" : "Завершение попытки возврата продавцу",
        orderId: returned.orderId,
        detail: returned.statusName,
      });
  }
  return events.sort((a, b) => (a.day ?? "9999").localeCompare(b.day ?? "9999"));
}

export default function OrderExemplarHistory({
  histories,
  currentOrderId,
}: {
  histories: OrderExemplarHistoryDto[];
  currentOrderId: string;
}) {
  const [selected, setSelected] = useState(histories[0]?.exemplarId ?? "");
  const history = histories.find((item) => item.exemplarId === selected) ?? histories[0];
  if (!history)
    return (
      <Alert severity="info">
        Exemplar ID ещё не получен. История появится после загрузки наклейки, начислений или
        возвратов WB.
      </Alert>
    );
  const events = eventsFor(history);
  const days = new Map<string | null, HistoryEvent[]>();
  for (const event of events) {
    const group = days.get(event.day) ?? [];
    group.push(event);
    days.set(event.day, group);
  }

  return (
    <Stack spacing={2}>
      <TextField
        select
        label="Exemplar ID (стикер WB)"
        value={history.exemplarId}
        onChange={(event) => setSelected(event.target.value)}
        fullWidth
      >
        {histories.map((item) => (
          <MenuItem key={item.exemplarId} value={item.exemplarId}>
            {item.exemplarId}
          </MenuItem>
        ))}
      </TextField>
      <Alert severity="info">
        Показаны известные события одного экземпляра в этом магазине. Даты событий — по Москве;
        порядок событий внутри одного дня не определён. Даты восстановленных заказов могут быть
        приблизительными.
      </Alert>
      {history.hasRestrictedHistory && (
        <Alert severity="warning">
          Часть связанных заказов недоступна. История и суммы включают только доступные операции.
        </Alert>
      )}
      <Stack spacing={2}>
        {[...days].map(([day, items]) => (
          <Box key={day ?? "unknown"}>
            <Typography variant="subtitle2" sx={{mb: 1}}>
              {day ? formatDateOnly(day) : "Дата не получена"}
            </Typography>
            <Stack spacing={1} sx={{borderLeft: 2, borderColor: "divider", pl: 2}}>
              {items.map((event) => {
                const order = history.orders.find((item) => item.id === event.orderId);
                return (
                  <Paper
                    key={event.key}
                    variant="outlined"
                    sx={{
                      p: 1.5,
                      borderColor: event.orderId === currentOrderId ? "primary.main" : "divider",
                    }}
                  >
                    <Stack
                      direction="row"
                      spacing={1}
                      useFlexGap
                      sx={{flexWrap: "wrap", alignItems: "center"}}
                    >
                      <Typography variant="body2" sx={{fontWeight: 600}}>
                        {event.label}
                      </Typography>
                      {event.orderId === currentOrderId && (
                        <Chip size="small" label="Текущий заказ" color="primary" />
                      )}
                    </Stack>
                    {order ? (
                      <>
                        <Link component={RouterLink} to={`/operations/orders/${order.id}`}>
                          {formatOrderNumber(order.number)} · {order.postingNumber}
                        </Link>
                        <Typography
                          variant="caption"
                          color="text.secondary"
                          sx={{display: "block"}}
                        >
                          {ORDER_TYPE_LABELS[order.type]} ·{" "}
                          {MARKETPLACE_ORDER_STATUS_LABELS[order.status]}
                          {order.isReconstructed && " · восстановлен по отчёту"}
                        </Typography>
                      </>
                    ) : (
                      <Typography variant="caption" color="text.secondary">
                        Без привязки к заказу
                      </Typography>
                    )}
                    {event.detail && (
                      <Typography
                        variant="body2"
                        color="text.secondary"
                        sx={{overflowWrap: "anywhere"}}
                      >
                        {event.detail}
                      </Typography>
                    )}
                  </Paper>
                );
              })}
            </Stack>
          </Box>
        ))}
      </Stack>
      {events.length === 0 && <Alert severity="info">Движения экземпляра ещё не получены.</Alert>}
      <FinanceAccrualsSection
        rows={history.accruals}
        title="Начисления за всю известную историю экземпляра"
      />
    </Stack>
  );
}
