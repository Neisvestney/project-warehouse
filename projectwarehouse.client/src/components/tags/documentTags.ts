import {
  ordersGetTagsOptions,
  receiptsGetTagsOptions,
  stocktakesGetTagsOptions,
  writeoffsGetTagsOptions,
} from "@/api/@tanstack/react-query.gen";
import {
  ordersCreateTag,
  receiptsCreateTag,
  stocktakesCreateTag,
  writeoffsCreateTag,
} from "@/api/sdk.gen";

/** Document types whose tags are managed through their own module endpoints. */
export type DocumentTagKind = "receipt" | "order" | "writeoff" | "stocktake";

export type DocumentTag = {id: string; name: string};

/** Every tag endpoint returns the same `{id, name}` list, so the per-kind options share one data type. */
export function documentTagsQueryOptions(kind: DocumentTagKind, search?: string) {
  const options = {query: {search}};
  switch (kind) {
    case "receipt":
      return receiptsGetTagsOptions(options);
    case "order":
      return ordersGetTagsOptions(options) as unknown as ReturnType<typeof receiptsGetTagsOptions>;
    case "writeoff":
      return writeoffsGetTagsOptions(options) as unknown as ReturnType<
        typeof receiptsGetTagsOptions
      >;
    case "stocktake":
      return stocktakesGetTagsOptions(options) as unknown as ReturnType<
        typeof receiptsGetTagsOptions
      >;
  }
}

export async function createDocumentTag(kind: DocumentTagKind, name: string): Promise<DocumentTag> {
  const options = {body: {name}, throwOnError: true} as const;
  switch (kind) {
    case "receipt":
      return (await receiptsCreateTag(options)).data;
    case "order":
      return (await ordersCreateTag(options)).data;
    case "writeoff":
      return (await writeoffsCreateTag(options)).data;
    case "stocktake":
      return (await stocktakesCreateTag(options)).data;
  }
}

/** Query key ids of the per-kind tag lists — invalidated when tags are renamed or deleted in settings. */
export const DOCUMENT_TAGS_QUERY_IDS = [
  "receiptsGetTags",
  "ordersGetTags",
  "writeoffsGetTags",
  "stocktakesGetTags",
];
