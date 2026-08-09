import type {StockMovementDirection} from "@/api/types.gen";

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
];

export const STOCK_MOVEMENT_DIRECTIONS: {value: StockMovementDirection; label: string}[] = [
  {value: "in", label: "Приход"},
  {value: "out", label: "Уход"},
  {value: "transferIn", label: "Перемещение (приход)"},
  {value: "transferOut", label: "Перемещение (уход)"},
];
