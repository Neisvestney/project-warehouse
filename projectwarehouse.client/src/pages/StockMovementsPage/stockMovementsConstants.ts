import type {StockMovementDirection} from "@/api/types.gen";

/**
 * Only these actions ever reach `StockMovement.Action` — every write goes through `InventoryService`,
 * whose callers either take the default or pass a `TransferActions` constant.
 */
export const STOCK_MOVEMENT_ACTIONS: {value: string; label: string}[] = [
  {value: "inventory.add_standard", label: "Добавление товара"},
  {value: "inventory.remove_standard", label: "Изъятие товара"},
  {value: "inventory.add_unit", label: "Добавление экземпляра"},
  {value: "inventory.remove_unit", label: "Изъятие экземпляра"},
  {value: "inventory.move_standard", label: "Внутреннее перемещение товара"},
  {value: "inventory.move_unit", label: "Внутреннее перемещение экземпляра"},
  {value: "transfer.standard", label: "Перемещение товара"},
  {value: "transfer.unit", label: "Перемещение экземпляра"},
];

export const STOCK_MOVEMENT_DIRECTIONS: {value: StockMovementDirection; label: string}[] = [
  {value: "in", label: "Приход"},
  {value: "out", label: "Уход"},
  {value: "transferIn", label: "Перемещение (приход)"},
  {value: "transferOut", label: "Перемещение (уход)"},
];
