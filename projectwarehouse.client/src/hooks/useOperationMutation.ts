import type {Attributes} from "@opentelemetry/api";
import {useMutation, type UseMutationOptions, type UseMutationResult} from "@tanstack/react-query";
import {withOperationSpan, type OperationScope} from "@/services/withOperationSpan";

/**
 * `useMutation` for a warehouse-state-changing operation: opens an operation span and hands the
 * request the scope that makes it a child of that span.
 *
 * ```ts
 * const mutation = useOperationMutation(
 *   "order.self_assign",
 *   {...ordersBatchSelfAssignMutation(), onSuccess},
 *   (v) => ({"order.count": v.body?.orderIds.length ?? 0}),
 * );
 * ```
 *
 * Covers the single-request case. An operation that sends several requests calls
 * {@link withOperationSpan} from the handler instead and spreads `op` into each of them — with
 * `mutateAsync`, so the span outlives them.
 */
export function useOperationMutation<
  TData,
  TError,
  TVariables extends Partial<OperationScope>,
  TContext,
>(
  name: string,
  options: UseMutationOptions<TData, TError, TVariables, TContext>,
  attributes?: (variables: TVariables) => Attributes,
): UseMutationResult<TData, TError, TVariables, TContext> {
  return useMutation({
    ...options,
    // Wrapping mutationFn rather than the caller's click handler is what makes the span cover the
    // request: mutate() neither returns nor waits, so a span opened around the call would already
    // have ended by the time anything was sent.
    mutationFn: (variables, mutationContext) =>
      withOperationSpan(name, attributes?.(variables) ?? {}, (op) =>
        options.mutationFn!({...variables, ...op}, mutationContext),
      ),
  });
}
