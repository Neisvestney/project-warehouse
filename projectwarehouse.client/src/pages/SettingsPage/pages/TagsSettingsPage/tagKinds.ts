import type {TagKind} from "@/api/types.gen";

export const ALL_TAG_KINDS: TagKind[] = [
  "receipt",
  "order",
  "writeoff",
  "stocktake",
  "catalogItem",
];

export const TAG_KIND_LABELS: Record<TagKind, string> = {
  receipt: "Приёмки",
  order: "Заказы",
  writeoff: "Списания",
  stocktake: "Инвентаризации",
  catalogItem: "Каталог",
};

/** What the objects bound to a tag of this kind are called in the delete warning. */
export const TAG_KIND_OBJECT_LABELS: Record<TagKind, string> = {
  receipt: "приёмок",
  order: "заказов",
  writeoff: "списаний",
  stocktake: "инвентаризаций",
  catalogItem: "позиций каталога",
};
