import type {StocktakeDifferenceResolution, StocktakeStatus, StocktakeType} from "@/api/types.gen";

export const STOCKTAKE_STATUS_LABELS: Record<StocktakeStatus, string> = {
  planned: "Запланирована",
  draft: "Черновик",
  inProgress: "В процессе",
  finished: "Завершена",
  canceled: "Отменена",
};

export const STOCKTAKE_TYPE_LABELS: Record<StocktakeType, string> = {
  unscheduled: "Внеплановая",
  scheduled: "Плановая",
};

export const DIFFERENCE_RESOLUTION_LABELS: Record<StocktakeDifferenceResolution, string> = {
  noChange: "Без изменений",
  surplus: "Оприходовать излишек",
  shortage: "Списать недостачу",
  relocation: "Переместить из другой ячейки",
  createUnit: "Создать экземпляр",
  detachUnit: "Открепить от ячейки",
  reattachUnit: "Вернуть в ячейку",
};

export function formatStocktakeNumber(n: number): string {
  return `ИНВ-${String(n).padStart(5, "0")}`;
}

export function deltaColor(delta: number): string {
  if (delta > 0) return "success.main";
  if (delta < 0) return "error.main";
  return "text.secondary";
}

export function formatDelta(delta: number): string {
  if (delta === 0) return "0";
  return delta > 0 ? `+${delta}` : String(delta);
}
