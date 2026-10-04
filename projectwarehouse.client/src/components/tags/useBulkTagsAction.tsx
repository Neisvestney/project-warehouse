import {useState} from "react";
import LocalOfferIcon from "@mui/icons-material/LocalOffer";
import type {UseMutationOptions} from "@tanstack/react-query";
import type {BatchUpdateTagsRequest, ErrorCode, TagBatchOperation} from "@/api/types.gen";
import type {BulkAction} from "@/components/BulkBar";
import BulkTagsDialog from "@/components/tags/BulkTagsDialog";
import {useOperationMutation} from "@/hooks/useOperationMutation";
import {useRetainedValue} from "@/hooks/useRetainedValue";
import type {OperationScope} from "@/services/withOperationSpan";
import {extractErrorMessage, isAppProblemDetails} from "@/utils/errorUtils";
import {pluralCount, type PluralForms} from "@/utils/pluralUtils";
import type {TagPickerKind} from "./documentTags";

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

/** Bulk «Теги» action for one module; `dialogs` must be rendered by the caller. */
export function useBulkTagsAction<TData, TError, TVariables extends BatchTagsVariables>({
  kind,
  entity,
  mutation: mutationOptions,
  invalidate,
  noun,
  notFound,
}: BulkTagsActionOptions<TData, TError, TVariables>) {
  // captured on click, so the dialog changes what was selected when it opened
  const [dialogIds, setDialogIds] = useState<string[] | null>(null);
  // keeps the count on screen through the closing animation
  const [shownIds] = useRetainedValue(dialogIds);
  const [error, setError] = useState<string | null>(null);

  // Names the documents that blocked the batch; `count` also covers the ones the user cannot see.
  function describeError(e: unknown): string {
    if (isAppProblemDetails(e)) {
      const args = e.errors.root?.find((x) => x.code === notFound.code)?.args;
      const count = Number(args?.count);
      if (Number.isFinite(count) && !notFound.numbersArg) {
        return `Не найдено: ${pluralCount(count, noun)}. Теги не изменены.`;
      }
      const numbers = notFound.numbersArg && args?.[notFound.numbersArg];
      if (Number.isFinite(count) && Array.isArray(numbers)) {
        const format = notFound.formatNumber ?? String;
        const parts = numbers.map((n) => format(Number(n)));
        const hidden = count - numbers.length;
        if (hidden > 0) parts.push(`ещё ${pluralCount(hidden, noun)}`);
        return `Не найдены или недоступны для редактирования: ${parts.join(", ")}. Теги не изменены.`;
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
        setDialogIds(null);
        await invalidate();
      },
      onError: (e) => setError(describeError(e)),
    },
    (variables) => ({
      [`${entity}.count`]: variables.body.ids.length,
      "tag.operation": variables.body.operation,
    }),
  );

  function handleConfirm(tagId: string, operation: TagBatchOperation) {
    if (!dialogIds) return;
    setError(null);
    // every module's generated variables are exactly `{body}` plus the optional request options
    mutation.mutate({body: {ids: dialogIds, tagId, operation}} as TVariables);
  }

  function handleClose() {
    setDialogIds(null);
    setError(null);
  }

  function getAction(ids: string[]): BulkAction {
    return {
      key: "updateTags",
      label: "Теги",
      icon: <LocalOfferIcon />,
      count: ids.length,
      pending: mutation.isPending,
      disabled: ids.length === 0 || mutation.isPending,
      onClick: () => setDialogIds(ids),
    };
  }

  const dialogs = (
    <BulkTagsDialog
      kind={kind}
      open={dialogIds != null}
      isPending={mutation.isPending}
      summary={`Будет затронуто: ${pluralCount(shownIds?.length ?? 0, noun)}.`}
      error={error}
      onClose={handleClose}
      onConfirm={handleConfirm}
    />
  );

  return {getAction, dialogs};
}
