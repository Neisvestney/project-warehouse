import type {WriteoffReason, WriteoffStatus} from "@/api/types.gen";

export const WRITEOFF_REASONS: WriteoffReason[] = ["loss", "defect", "consumption", "other"];

export const WRITEOFF_REASON_LABELS: Record<WriteoffReason, string> = {
  loss: "Потеря",
  defect: "Брак",
  consumption: "Расход",
  other: "Прочее",
};

export const WRITEOFF_STATUS_LABELS: Record<WriteoffStatus, string> = {
  draft: "Черновик",
  finished: "Завершено",
  canceled: "Отменено",
};

export function formatWriteoffNumber(n: number): string {
  return `СПС-${String(n).padStart(5, "0")}`;
}
