import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableRow,
  Typography,
} from "@mui/material";
import type {MarketplaceSyncRunDto} from "@/api/types.gen";
import {useBackClosable} from "@/hooks/useBackClosable";
import {useRetainedValue} from "@/hooks/useRetainedValue";
import SkippedOrdersList from "@/components/orders/marketplace/SkippedOrdersList";
import SyncErrorAlert from "../../components/SyncErrorAlert";
import {
  SYNC_SCOPE_LABELS,
  SYNC_SCOPE_SECTIONS,
  type SyncRunSection,
  formatDateTime,
} from "../../marketplaceUtils";

const SECTION_TITLES: Record<SyncRunSection, string> = {
  warehouses: "Склады",
  cards: "Карточки",
  orders: "Заказы",
  returns: "Возвраты",
};

function sectionCounters(run: MarketplaceSyncRunDto, section: SyncRunSection): [string, number][] {
  switch (section) {
    case "warehouses":
      return [["Обработано", run.warehousesProcessed]];
    case "cards":
      return [
        ["Обработано", run.cardsProcessed],
        ["Создано", run.cardsCreated],
        ["Обновлено", run.cardsUpdated],
        ["Архивировано", run.cardsArchived],
        ["Автосопоставлено", run.autoMapped],
      ];
    case "orders":
      return [
        ["Обработано", run.ordersProcessed],
        ["Создано", run.ordersCreated],
        ["Обновлено", run.ordersUpdated],
        ["Пропущено", run.ordersSkipped],
      ];
    case "returns":
      return [
        ["Обработано", run.returnsProcessed],
        ["Создано", run.returnsCreated],
        ["Обновлено", run.returnsUpdated],
      ];
  }
}

interface SyncRunDetailsDialogProps {
  run: MarketplaceSyncRunDto | null;
  onClose: () => void;
}

function SyncRunDetailsDialog({run, onClose}: SyncRunDetailsDialogProps) {
  const [shown, releaseShown] = useRetainedValue(run);

  useBackClosable(run !== null, onClose);

  return (
    <Dialog
      open={run !== null}
      onClose={onClose}
      fullWidth
      maxWidth="sm"
      slotProps={{transition: {onExited: releaseShown}}}
    >
      <DialogTitle>
        {shown && `${SYNC_SCOPE_LABELS[shown.scope]} — ${formatDateTime(shown.startedAt)}`}
      </DialogTitle>
      <DialogContent dividers>
        {shown && (
          <Stack spacing={2}>
            <SyncErrorAlert error={shown.error} title="Запуск завершился ошибкой" />
            {SYNC_SCOPE_SECTIONS[shown.scope].map((section) => (
              <Stack key={section} spacing={0.5}>
                <Typography variant="subtitle2">{SECTION_TITLES[section]}</Typography>
                <Table size="small">
                  <TableBody>
                    {sectionCounters(shown, section).map(([label, value]) => (
                      <TableRow key={label}>
                        <TableCell>{label}</TableCell>
                        <TableCell align="right">{value}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
                {section === "orders" && (
                  <SkippedOrdersList items={shown.skippedOrders} total={shown.ordersSkipped} />
                )}
              </Stack>
            ))}
          </Stack>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Закрыть</Button>
      </DialogActions>
    </Dialog>
  );
}

export default SyncRunDetailsDialog;
