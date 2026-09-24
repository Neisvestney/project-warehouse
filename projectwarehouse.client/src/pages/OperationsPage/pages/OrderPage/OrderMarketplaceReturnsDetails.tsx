import {useState} from "react";
import {Button, Chip, Collapse, Paper, Stack, Typography} from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import ExpandLessIcon from "@mui/icons-material/ExpandLess";
import {format} from "date-fns";
import {ru} from "date-fns/locale";
import type {MarketplaceReturnDto, OrderDetailsDto} from "@/api/types.gen";
import InfoRow from "@/components/InfoRow";
import {
  MARKETPLACE_RETURN_COMPENSATION_LABELS,
  MARKETPLACE_RETURN_KIND_LABELS,
} from "@/components/orders/marketplace/marketplaceOrderUtils";

function formatDate(iso: string | null | undefined): string | null {
  if (!iso) return null;
  try {
    return format(new Date(iso), "d MMM yyyy, HH:mm", {locale: ru});
  } catch {
    return iso;
  }
}

function formatPrice(value: MarketplaceReturnDto): string | null {
  if (value.price == null) return null;
  if (!value.currencyCode) return value.price.toLocaleString("ru-RU");
  try {
    return value.price.toLocaleString("ru-RU", {style: "currency", currency: value.currencyCode});
  } catch {
    // malformed currency code throws RangeError
    return `${value.price.toLocaleString("ru-RU")} ${value.currencyCode}`;
  }
}

function notCountedReason(value: MarketplaceReturnDto): string {
  if (value.isCancelled) return "Заявка отменена — товар остался у покупателя";
  if (value.kind === "unknown") return "Вид возврата не распознан";
  return "Отмена отправления, а не возврат";
}

interface ReturnCardProps {
  value: MarketplaceReturnDto;
  productName: string;
}

function ReturnCard({value, productName}: ReturnCardProps) {
  const price = formatPrice(value);
  return (
    <Paper variant="outlined" sx={{p: 1.5, opacity: value.isCountedAsReturn ? 1 : 0.6}}>
      <Stack spacing={0.5}>
        <Stack direction="row" spacing={1} sx={{alignItems: "center", flexWrap: "wrap"}}>
          <Typography variant="body2" sx={{fontWeight: 600}}>
            {MARKETPLACE_RETURN_KIND_LABELS[value.kind]}
          </Typography>
          {!value.isCountedAsReturn && <Chip size="small" variant="outlined" label="Не засчитан" />}
        </Stack>
        {!value.isCountedAsReturn && (
          <Typography variant="caption" color="text.secondary">
            {notCountedReason(value)}
          </Typography>
        )}
        <InfoRow label="Товар" value={productName} />
        <InfoRow label="Количество" value={`${value.quantity} шт.`} />
        {price && <InfoRow label="Цена" value={price} />}
        {value.reason && <InfoRow label="Причина" value={value.reason} />}
        <InfoRow label="Статус" value={value.statusName ?? value.rawStatus ?? "—"} />
        {value.returnedAt && <InfoRow label="Дата возврата" value={formatDate(value.returnedAt)} />}
        {value.finalAt && <InfoRow label="Прибыл на склад" value={formatDate(value.finalAt)} />}
        {value.compensationStatus && (
          <InfoRow
            label="Компенсация"
            value={`${MARKETPLACE_RETURN_COMPENSATION_LABELS[value.compensationStatus]}${
              value.compensationStatusChangedAt
                ? `, ${formatDate(value.compensationStatusChangedAt)}`
                : ""
            }`}
          />
        )}
      </Stack>
    </Paper>
  );
}

interface OrderMarketplaceReturnsDetailsProps {
  order: OrderDetailsDto;
}

function OrderMarketplaceReturnsDetails({order}: OrderMarketplaceReturnsDetailsProps) {
  const [open, setOpen] = useState(false);
  const returns = order.marketplaceReturns;
  if (returns.length === 0) return null;

  const countedQuantity = returns
    .filter((r) => r.isCountedAsReturn)
    .reduce((sum, r) => sum + r.quantity, 0);
  const itemNames = new Map(
    order.marketplaceItems.map((i) => [i.id, i.marketplaceCard?.name ?? null]),
  );

  return (
    <>
      <InfoRow
        label="Возвраты"
        value={
          <Button
            size="small"
            onClick={() => setOpen((v) => !v)}
            endIcon={open ? <ExpandLessIcon /> : <ExpandMoreIcon />}
          >
            {countedQuantity > 0 ? `Вернули ${countedQuantity} шт.` : "Нет засчитанных"} · записей:{" "}
            {returns.length}
          </Button>
        }
      />
      <Collapse in={open} unmountOnExit>
        <Stack spacing={1}>
          {returns.map((r) => (
            <ReturnCard
              key={r.id}
              value={r}
              productName={
                (r.orderMarketplaceItemId && itemNames.get(r.orderMarketplaceItemId)) || r.offerId
              }
            />
          ))}
        </Stack>
      </Collapse>
    </>
  );
}

export default OrderMarketplaceReturnsDetails;
