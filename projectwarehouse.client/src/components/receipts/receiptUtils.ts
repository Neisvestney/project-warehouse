import type {
  ReceiptItemDto,
  ReceiptItemPlacementDto,
  ReceiptReason,
  ReceiptStatus,
} from "@/api/types.gen";

export const RECEIPT_REASON_LABELS: Record<ReceiptReason, string> = {
  newGoods: "Новые товары",
  return: "Возврат",
  other: "Прочее",
};

export function formatReceiptNumber(n: number): string {
  return `ПРХ-${String(n).padStart(5, "0")}`;
}

export const RECEIPT_STATUS_LABELS: Record<ReceiptStatus, string> = {
  draft: "Черновик",
  planned: "Запланирована",
  processing: "Обрабатывается",
  finished: "Завершена",
  canceled: "Отменена",
};

/** Units one placement stands for: a Unit placement carries no count and always means one. */
export function placedUnits(placement: ReceiptItemPlacementDto): number {
  return placement.count || (placement.unitInventoryItemId ? 1 : 0);
}

export function calcTotalPlaced(item: ReceiptItemDto): number {
  return item.placements.reduce((sum, p) => sum + placedUnits(p), 0);
}

/** How much of an item is still waiting for a cell. */
export function calcRemainingToPlace(item: ReceiptItemDto): number {
  return (item.receivedCount ?? item.plannedCount) - calcTotalPlaced(item);
}

export interface AutoAcceptPlan {
  /** Standard items that get a received count, a top-up placement, or both. */
  affectedCount: number;
  /** Of those, the ones whose received count is still unset and becomes the planned count. */
  filledCount: number;
  /** Total quantity that goes into the default node. */
  placedCount: number;
  /** Serialised items still waiting for a cell — auto-accept cannot place them. */
  unitCount: number;
  /** Standard items placed beyond their target, which auto-accept leaves for manual sorting out. */
  overplacedCount: number;
}

/** Mirrors POST /api/receipts/{id}/auto-accept so the confirmation can state what will happen. */
export function buildAutoAcceptPlan(items: ReceiptItemDto[]): AutoAcceptPlan {
  const plan: AutoAcceptPlan = {
    affectedCount: 0,
    filledCount: 0,
    placedCount: 0,
    unitCount: 0,
    overplacedCount: 0,
  };

  for (const item of items) {
    const remaining = calcRemainingToPlace(item);

    if (item.catalogItem.type === "unit") {
      if (remaining > 0) plan.unitCount += 1;
      continue;
    }
    if (item.catalogItem.type !== "standard") continue;

    if (remaining < 0) {
      plan.overplacedCount += 1;
      continue;
    }

    const needsCount = item.receivedCount === null || item.receivedCount === undefined;
    const target = item.receivedCount ?? item.plannedCount;
    if (remaining === 0 && (!needsCount || target === 0)) continue;

    plan.affectedCount += 1;
    if (needsCount) plan.filledCount += 1;
    plan.placedCount += remaining;
  }

  return plan;
}
