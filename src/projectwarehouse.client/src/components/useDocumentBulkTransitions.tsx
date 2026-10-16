import {useState, type ReactNode} from "react";
import {Alert, Typography} from "@mui/material";
import type {UseMutationOptions} from "@tanstack/react-query";
import type {
  DocumentBatchTransitionFailedItem,
  DocumentBatchTransitionResponse,
} from "@/api/types.gen";
import type {BulkAction} from "@/components/BulkBar";
import ConfirmDialog from "@/components/ConfirmDialog";
import {useOperationMutation} from "@/hooks/useOperationMutation";
import type {OperationScope} from "@/services/withOperationSpan";
import {extractErrorMessage, resolveErrorMessage} from "@/utils/errorUtils";
import {pluralCount, type PluralForms} from "@/utils/pluralUtils";

export interface DocumentBulkTransition<TStatus extends string, TTransition extends string> {
  transition: TTransition;
  /** Statuses the transition applies to; the rest of the selection is left out of the request. */
  from: TStatus[];
  label: string;
  /** Completes «Часть … не удалось …:». */
  failedVerb: string;
  icon: ReactNode;
  /** Primary transitions get their own toolbar button, the rest go into the «Ещё» menu. */
  primary?: boolean;
  danger?: boolean;
  confirm?: {title: string; text: string; confirmText: string};
}

type BatchTransitionVariables<TTransition extends string> = {
  body: {ids: string[]; transition: TTransition};
} & Partial<OperationScope>;

interface DocumentBulkTransitionsOptions<
  TItem extends {id: string; status: TStatus},
  TStatus extends string,
  TTransition extends string,
  TVariables extends BatchTransitionVariables<TTransition>,
  TError,
> {
  /** Operation span prefix, e.g. `receipt` → span `receipt.batch_transition`. */
  entity: string;
  transitions: DocumentBulkTransition<TStatus, TTransition>[];
  selectedItems: TItem[];
  /** False hides every transition — the caller's edit permission. */
  enabled: boolean;
  /** The module's generated `…BatchTransitionMutation()`. */
  mutation: UseMutationOptions<DocumentBatchTransitionResponse, TError, TVariables>;
  invalidate: () => Promise<unknown>;
  /** Drops the transitioned documents from the selection. */
  onTransitioned: (ids: string[]) => void;
  noun: PluralForms;
  formatNumber: (n: number) => string;
}

/**
 * Status transitions of a document list's selection as `BulkBar` actions. `dialogs` holds the confirmation
 * and `alerts` the per-document failures; the caller renders both.
 */
export function useDocumentBulkTransitions<
  TItem extends {id: string; status: TStatus},
  TStatus extends string,
  TTransition extends string,
  TVariables extends BatchTransitionVariables<TTransition>,
  TError,
>({
  entity,
  transitions,
  selectedItems,
  enabled,
  mutation: mutationOptions,
  invalidate,
  onTransitioned,
  noun,
  formatNumber,
}: DocumentBulkTransitionsOptions<TItem, TStatus, TTransition, TVariables, TError>) {
  type Transition = DocumentBulkTransition<TStatus, TTransition>;

  const [failed, setFailed] = useState<{
    verb: string;
    items: DocumentBatchTransitionFailedItem[];
  } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [active, setActive] = useState<Transition | null>(null);
  const [confirming, setConfirming] = useState<Transition | null>(null);

  const mutation = useOperationMutation(
    `${entity}.batch_transition`,
    {
      ...mutationOptions,
      meta: {suppressGlobalError: true},
      // Awaited so isPending covers the refetch and the button cannot re-send stale ids
      onSuccess: async (data) => {
        onTransitioned([...data.transitionedIds]);
        await invalidate();
      },
      onError: (e) => setError(extractErrorMessage(e)),
    },
    (variables) => ({
      [`${entity}.count`]: variables.body.ids.length,
      [`${entity}.transition`]: variables.body.transition,
    }),
  );

  const idsFor = (transition: Transition) =>
    selectedItems.filter((item) => transition.from.includes(item.status)).map((item) => item.id);

  function run(transition: Transition) {
    const ids = idsFor(transition);
    if (ids.length === 0) return;
    setFailed(null);
    setError(null);
    setActive(transition);
    // every module's generated variables are exactly `{body}` plus the optional request options
    mutation.mutate({body: {ids, transition: transition.transition}} as TVariables, {
      onSuccess: (data) =>
        setFailed(
          data.failedItems.length > 0
            ? {verb: transition.failedVerb, items: [...data.failedItems]}
            : null,
        ),
      onSettled: () => {
        setConfirming(null);
        setActive(null);
      },
    });
  }

  const actions: BulkAction[] = enabled
    ? transitions
        .map((transition) => ({transition, count: idsFor(transition).length}))
        .filter(({count}) => count > 0)
        .map(({transition, count}) => ({
          key: transition.transition,
          label: transition.label,
          icon: transition.icon,
          count,
          primary: transition.primary,
          danger: transition.danger,
          pending: mutation.isPending && active?.transition === transition.transition,
          disabled: mutation.isPending,
          onClick: () => (transition.confirm ? setConfirming(transition) : run(transition)),
        }))
    : [];

  const fallbackName = noun.one.charAt(0).toUpperCase() + noun.one.slice(1);

  const dialogs = confirming?.confirm && (
    <ConfirmDialog
      open
      onClose={() => setConfirming(null)}
      title={confirming.confirm.title}
      onConfirm={() => run(confirming)}
      isPending={mutation.isPending}
      confirmText={confirming.confirm.confirmText}
      confirmColor={confirming.danger ? "error" : "primary"}
    >
      <Typography variant="body2" sx={{mb: 1}}>
        Будет затронуто: {pluralCount(idsFor(confirming).length, noun)}.
      </Typography>
      <Typography variant="body2">{confirming.confirm.text}</Typography>
    </ConfirmDialog>
  );

  const alerts = (
    <>
      {failed && (
        <Alert severity="error" onClose={() => setFailed(null)}>
          <Typography variant="body2" sx={{mb: 0.5}}>
            Часть {noun.many} не удалось {failed.verb}:
          </Typography>
          {failed.items.map((f) => (
            <Typography key={f.id} variant="caption" sx={{display: "block"}}>
              • {f.number != null ? formatNumber(f.number) : fallbackName}:{" "}
              {resolveErrorMessage(f.error)}
            </Typography>
          ))}
        </Alert>
      )}
      {error && (
        <Alert severity="error" onClose={() => setError(null)}>
          {error}
        </Alert>
      )}
    </>
  );

  return {actions, dialogs, alerts};
}
