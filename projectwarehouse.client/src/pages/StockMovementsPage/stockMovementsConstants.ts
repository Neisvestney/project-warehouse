import type {StockMovementDirection} from "@/api/types.gen";

/** Mirrors `StockMovementReportPresetLimits.MaxMetrics` — the server rejects a longer list. */
export const MAX_METRICS = 12;

/**
 * Only these actions ever reach `StockMovement.Action` — every write goes through `InventoryService`,
 * whose callers either take the default or pass a `TransferActions` constant.
 */
export const STOCK_MOVEMENT_ACTIONS: {value: string; label: string}[] = [
  {value: "inventory.new_goods", label: "Добавление нового товара"},
  {value: "inventory.return_stock", label: "Возвраты"},
  {value: "inventory.written_off", label: "Списания"},
  {value: "inventory.spent_on_order", label: "Заказы"},
  {value: "inventory.move_stock", label: "Перемещение товара"},
  {value: "inventory.canceled_fulfillment", label: "Отмена фулфилментов"},
  {value: "inventory.canceled_placement", label: "Отмена размещений (приёмка)"},
  {value: "inventory.stocktake_surplus", label: "Инвентаризация — излишки"},
  {value: "inventory.stocktake_shortage", label: "Инвентаризация — недостачи"},
  {value: "inventory.stocktake_relocation", label: "Инвентаризация — перемещение"},
  {value: "transfer.standard", label: "Перемещение — обычный товар"},
  {value: "transfer.unit", label: "Перемещение — учётная единица"},
];

export const STOCK_MOVEMENT_DIRECTIONS: {value: StockMovementDirection; label: string}[] = [
  {value: "in", label: "Приход"},
  {value: "out", label: "Уход"},
  {value: "transferIn", label: "Перемещение (приход)"},
  {value: "transferOut", label: "Перемещение (уход)"},
];
