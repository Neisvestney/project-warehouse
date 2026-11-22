import {useState} from "react";
import LocalOfferIcon from "@mui/icons-material/LocalOffer";
import {enqueueSnackbar} from "notistack";
import type {UseMutationOptions} from "@tanstack/react-query";
import type {BatchUpdateTagsRequest, ErrorCode, TagBatchOperation} from "@/api/types.gen";
import type {BulkAction} from "@/components/BulkBar";
import BulkTagsMenu, {type BulkTagsSelection} from "@/components/tags/BulkTagsMenu";
import {useOperationMutation} from "@/hooks/useOperationMutation";
import type {OperationScope} from "@/services/withOperationSpan";
import {extractErrorMessage, isAppProblemDetails} from "@/utils/errorUtils";
import {pluralCount, type PluralForms} from "@/utils/pluralUtils";
import type {DocumentTag, TagPickerKind} from "./documentTags";

type BatchTagsVariables = {body: BatchUpdateTagsRequest} & Partial<OperationScope>;

interface BulkTagsActionOptions<TData, TError, TVariables extends BatchTagsVariables> {
  kind: TagPickerKind;
  /** Operation span name and the prefix of its `.count` attribute, e.g. `order` → `order.update_tags`. */
  entity: string;
  /** The module's generated `…BatchUpdateTagsMutation()`. */
  mutation: UseMutationOptions<TData, TError, TVariables>;
  invalidate: () => Promise<unknown>;
  noun: PluralForms;
  /** The module's 404 code and, for documents, the arg that lists the blocking documents' numbers. */
  notFound: {code: ErrorCode; numbersArg?: string; formatNumber?: (n: number) => string};
}

/** Bulk tag action; callers render the menu through `dialogs`. */
export function useBulkTagsAction<TData, TError, TVariables extends BatchTagsVariables>({
  kind,
  entity,
  mutation: mutationOptions,
  invalidate,
  noun,
  notFound,
}: BulkTagsActionOptions<TData, TError, TVariables>) {
  const [selection, setSelection] = useState<BulkTagsSelection | null>(null);
  const [error, setError] = useState<string | null>(null);

  // Names the documents that blocked the batch; `count` also covers the ones the user cannot see.
  function describeError(e: unknown): string {
    if (isAppProblemDetails(e)) {
      const args = e.errors.root?.find((x) => x.code === notFound.code)?.args;
      const count = Number(args?.count);
      if (Number.isFinite(count) && !notFound.numbersArg) {
        return `Не найдено: ${pluralCount(count, noun)}. Метки не изменены.`;
      }
      const numbers = notFound.numbersArg && args?.[notFound.numbersArg];
      if (Number.isFinite(count) && Array.isArray(numbers)) {
        const format = notFound.formatNumber ?? String;
        const parts = numbers.map((n) => format(Number(n)));
        const hidden = count - numbers.length;
        if (hidden > 0) parts.push(`ещё ${pluralCount(hidden, noun)}`);
        return `Не найдены или недоступны для редактирования: ${parts.join(", ")}. Метки не изменены.`;
      }
    }
    return extractErrorMessage(e);
  }

  const mutation = useOperationMutation(
    `${entity}.update_tags`,
    {
      ...mutationOptions,
      meta: {suppressGlobalError: true},
      onSuccess: async () => {
        setSelection(null);
        await invalidate();
      },
      onError: (e) => setError(describeError(e)),
    },
    (variables) => ({
      [`${entity}.count`]: variables.body.ids.length,
      "tag.operation": variables.body.operation,
    }),
  );

  function handleConfirm(tag: DocumentTag, operation: TagBatchOperation) {
    if (!selection || mutation.isPending) return;
    setError(null);
    // every module's generated variables are exactly `{body}` plus the optional request options
    mutation.mutate({body: {ids: selection.ids, tagId: tag.id, operation}} as TVariables, {
      onSuccess: () =>
        enqueueSnackbar(
          `Метка «${tag.name}» ${operation === "add" ? "добавлена" : "снята"}: ${pluralCount(selection.ids.length, noun)}.`,
          {variant: "success"},
        ),
    });
  }

  function handleClose() {
    if (mutation.isPending) return;
    setSelection(null);
    setError(null);
  }

  function getAction(ids: string[]): BulkAction {
    return {
      key: "updateTags",
      label: "Метки",
      icon: <LocalOfferIcon />,
      count: ids.length,
      pending: mutation.isPending,
      disabled: ids.length === 0 || mutation.isPending,
      onClick: (event) => {
        const rect = event.currentTarget.getBoundingClientRect();
        setError(null);
        const position =
          event.detail === 0
            ? {top: rect.bottom, left: rect.left}
            : {top: event.clientY, left: event.clientX};
        setSelection({ids: [...ids], position});
      },
    };
  }

  const dialogs = (
    <BulkTagsMenu
      kind={kind}
      selection={selection}
      isPending={mutation.isPending}
      summary={`Выбрано: ${pluralCount(selection?.ids.length ?? 0, noun)}.`}
      error={error}
      onClose={handleClose}
      onConfirm={handleConfirm}
    />
  );

  return {getAction, dialogs};
}
