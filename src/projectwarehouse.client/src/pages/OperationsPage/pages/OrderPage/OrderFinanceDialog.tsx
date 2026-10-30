import {useState} from "react";
import {useQuery} from "@tanstack/react-query";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogContent,
  DialogTitle,
  IconButton,
  Paper,
  Stack,
  Tab,
  Tabs,
  Typography,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import {orderFinanceGetOptions} from "@/api/@tanstack/react-query.gen";
import type {OrderDetailsDto} from "@/api/types.gen";
import {useRetainedValue} from "@/hooks/useRetainedValue";
import {extractErrorMessage} from "@/utils/errorUtils";
import {formatOrderNumber} from "@/components/orders/orderUtils";
import FinanceAccrualsSection from "./FinanceAccrualsSection";
import OrderExemplarHistory from "./OrderExemplarHistory";

export default function OrderFinanceDialog({
  order,
  open,
  onClose,
}: {
  order: OrderDetailsDto;
  open: boolean;
  onClose: () => void;
}) {
  const [shown, release] = useRetainedValue(open ? order : null);
  const isMobile = useMediaQuery(useTheme().breakpoints.down("sm"));
  return (
    <Dialog
      open={open}
      onClose={onClose}
      fullWidth
      maxWidth="lg"
      fullScreen={isMobile}
      aria-labelledby="order-finance-title"
      slotProps={{
        transition: {onExited: release},
        paper: {sx: {pointerEvents: open ? undefined : "none", height: {sm: "85vh"}}},
      }}
    >
      {shown && <OrderFinanceContent key={shown.id} order={shown} onClose={onClose} />}
    </Dialog>
  );
}

function OrderFinanceContent({order, onClose}: {order: OrderDetailsDto; onClose: () => void}) {
  const [tab, setTab] = useState("money");
  const query = useQuery({
    ...orderFinanceGetOptions({path: {id: order.id}}),
    meta: {suppressGlobalError: true, suppressGlobalNotFound: true},
  });
  const isWb = order.marketplaceOrder?.marketplaceType === "wildberries";
  return (
    <>
      <DialogTitle
        id="order-finance-title"
        sx={{display: "flex", alignItems: "center", gap: 1, pr: 1}}
      >
        <Box sx={{flexGrow: 1}}>
          Финансы и история
          <Typography variant="body2" color="text.secondary">
            {formatOrderNumber(order.number)} · {order.marketplaceOrder?.postingNumber}
          </Typography>
        </Box>
        <IconButton onClick={onClose} aria-label="Закрыть">
          <CloseIcon />
        </IconButton>
      </DialogTitle>
      <Tabs
        value={tab}
        onChange={(_, value: string) => setTab(value)}
        variant="scrollable"
        sx={{px: 2}}
      >
        <Tab
          value="money"
          label="Деньги заказа"
          id="order-money-tab"
          aria-controls="order-money-panel"
        />
        {isWb && (
          <Tab
            value="history"
            label="История экземпляра"
            id="order-history-tab"
            aria-controls="order-history-panel"
          />
        )}
      </Tabs>
      <DialogContent dividers>
        <Stack spacing={2}>
          {query.isFetching && !query.data && (
            <Box sx={{textAlign: "center", p: 4}}>
              <CircularProgress aria-label="Загрузка финансов" />
            </Box>
          )}
          {query.isError && (
            <Alert
              severity="error"
              action={
                <Button color="inherit" onClick={() => void query.refetch()}>
                  Повторить
                </Button>
              }
            >
              {extractErrorMessage(query.error)}
            </Alert>
          )}
          {query.data &&
            (tab === "money" ? (
              <Box role="tabpanel" id="order-money-panel" aria-labelledby="order-money-tab">
                <Stack spacing={2}>
                  <Typography variant="subtitle1" sx={{fontWeight: 600}}>
                    Цены в заказе
                  </Typography>
                  {order.marketplaceItems.map((item) => {
                    const price = (
                      amount: number | null | undefined,
                      currency: string | null | undefined,
                    ) =>
                      amount == null
                        ? "Не получена"
                        : `${amount.toLocaleString("ru-RU", {minimumFractionDigits: 2, maximumFractionDigits: 2})} ${currency || "(валюта не указана)"}`;
                    return (
                      <Paper key={item.id} variant="outlined" sx={{p: 1.5}}>
                        <Typography variant="body2">
                          {item.marketplaceCard?.name || "Позиция заказа"} · {item.quantity} шт.
                        </Typography>
                        <Typography variant="body2" color="text.secondary">
                          Цена продавца за штуку: {price(item.price, item.currencyCode)}
                        </Typography>
                        <Typography variant="body2" color="text.secondary">
                          Цена покупателя по данным площадки:{" "}
                          {price(item.customerPrice, item.customerCurrencyCode)}
                        </Typography>
                      </Paper>
                    );
                  })}
                  <FinanceAccrualsSection
                    rows={query.data.accruals}
                    title="Начисления по этому заказу"
                  />
                </Stack>
              </Box>
            ) : (
              <Box role="tabpanel" id="order-history-panel" aria-labelledby="order-history-tab">
                <OrderExemplarHistory histories={query.data.exemplars} currentOrderId={order.id} />
              </Box>
            ))}
        </Stack>
      </DialogContent>
    </>
  );
}
