import type {TagKind} from "@/api/types.gen";

export const ALL_TAG_KINDS: TagKind[] = ["receipt", "catalogItem"];

export const TAG_KIND_LABELS: Record<TagKind, string> = {
  receipt: "Приёмки",
  catalogItem: "Каталог",
};

/** What the objects bound to a tag of this kind are called in the delete warning. */
export const TAG_KIND_OBJECT_LABELS: Record<TagKind, string> = {
  receipt: "приёмок",
  catalogItem: "позиций каталога",
};
