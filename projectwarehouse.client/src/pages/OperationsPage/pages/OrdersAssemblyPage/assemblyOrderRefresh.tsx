import {createContext, useContext, useRef, useState} from "react";
import {useQueryClient} from "@tanstack/react-query";
import {ordersGetAssemblyById} from "@/api/sdk.gen";
import type {OrderDetailsDto} from "@/api/types.gen";
import {byOperation} from "@/utils/queryKeys";
import {isNotFoundError} from "@/utils/errorUtils";

export interface AssemblyOrderRefresh {
  /** Rereads one order into the cached list; a task id puts the overlay on that task instead of the order. */
  refreshOrder: (orderId: string, taskId?: string | null) => Promise<void>;
  isOrderRefreshing: (orderId: string) => boolean;
  isTaskRefreshing: (taskId: string) => boolean;
}

const AssemblyOrderRefreshContext = createContext<AssemblyOrderRefresh | null>(null);

export const AssemblyOrderRefreshProvider = AssemblyOrderRefreshContext.Provider;

export function useAssemblyOrderRefresh(): AssemblyOrderRefresh {
  const value = useContext(AssemblyOrderRefreshContext);
  if (!value) throw new Error("useAssemblyOrderRefresh must be used inside OrdersAssemblyPage");
  return value;
}

/**
 * Owned by the page: patches single orders into every cached `ordersGetAllAssembly` list instead of
 * refetching it, so a change to one order dims only that order.
 */
export function useAssemblyOrderRefreshState(): AssemblyOrderRefresh {
  const queryClient = useQueryClient();
  const [pending, setPending] = useState<ReadonlyMap<string, number>>(new Map());
  // A later request for the same order wins even if its response lands first.
  const seqRef = useRef(new Map<string, number>());

  function bump(key: string, delta: number) {
    setPending((prev) => {
      const next = new Map(prev);
      const count = (next.get(key) ?? 0) + delta;
      if (count > 0) next.set(key, count);
      else next.delete(key);
      return next;
    });
  }

  function patchLists(orderId: string, order: OrderDetailsDto | null) {
    queryClient.setQueriesData<OrderDetailsDto[]>(
      {queryKey: byOperation("ordersGetAllAssembly")},
      (list) =>
        list?.flatMap((o) => {
          if (o.id !== orderId) return [o];
          return order ? [order] : [];
        }),
    );
  }

  async function refreshOrder(orderId: string, taskId?: string | null) {
    const key = taskId ? `task:${taskId}` : `order:${orderId}`;
    const seq = (seqRef.current.get(orderId) ?? 0) + 1;
    seqRef.current.set(orderId, seq);
    bump(key, 1);
    try {
      const {data} = await ordersGetAssemblyById({path: {id: orderId}, throwOnError: true});
      if (seqRef.current.get(orderId) === seq) patchLists(orderId, data);
    } catch (error) {
      if (seqRef.current.get(orderId) !== seq) return;
      // 404 means the order is no longer on this user's list; anything else falls back to the full list
      if (isNotFoundError(error)) patchLists(orderId, null);
      else void queryClient.invalidateQueries({queryKey: byOperation("ordersGetAllAssembly")});
    } finally {
      bump(key, -1);
    }
  }

  return {
    refreshOrder,
    isOrderRefreshing: (orderId) => pending.has(`order:${orderId}`),
    isTaskRefreshing: (taskId) => pending.has(`task:${taskId}`),
  };
}
