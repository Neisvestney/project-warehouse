import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";
import {
  stockMovementPresetsCreatePresetMutation,
  stockMovementPresetsDeletePresetMutation,
  stockMovementPresetsGetPresetsOptions,
  stockMovementPresetsGetPresetsQueryKey,
  stockMovementPresetsUpdatePresetMutation,
} from "@/api/@tanstack/react-query.gen";
import type {StockMovementReportPresetDto} from "@/api/types.gen";

/**
 * The shared preset list plus the three writes against it. Presets are global, so every mutation
 * invalidates the one list query and everyone picks the change up on their next fetch.
 */
export function useStockMovementPresets(presetId: string | null) {
  const queryClient = useQueryClient();
  const listKey = stockMovementPresetsGetPresetsQueryKey();

  const query = useQuery(stockMovementPresetsGetPresetsOptions());
  const presets = query.data ?? [];

  // `?preset=` wins, then the one flagged default, then whatever came first — the report needs a
  // preset to have any columns at all, so there is no "nothing selected" state.
  const active: StockMovementReportPresetDto | undefined =
    presets.find((p) => p.id === presetId) ?? presets.find((p) => p.isDefault) ?? presets[0];

  const invalidate = () => queryClient.invalidateQueries({queryKey: listKey});

  const create = useMutation({
    ...stockMovementPresetsCreatePresetMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (created) => {
      // Put the new preset in the list before the refetch lands: the caller selects it immediately, and
      // a list that does not contain it yet would fall back to the default for a frame.
      queryClient.setQueryData(listKey, (old: StockMovementReportPresetDto[] | undefined) =>
        old ? [...old, created] : [created],
      );
      return invalidate();
    },
  });

  const update = useMutation({
    ...stockMovementPresetsUpdatePresetMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: invalidate,
  });

  const remove = useMutation({
    ...stockMovementPresetsDeletePresetMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: invalidate,
  });

  return {presets, active, isLoading: query.isLoading, error: query.error, create, update, remove};
}
