import type {AssemblyTaskStatus, OrderDetailsDto, OrderType} from "@/api/types.gen";
import {ORDER_TYPE_LABELS} from "@/components/orders/orderUtils";
import {TASK_STATUS_LABELS} from "@/components/orders/orderAssemblyUtils";

export type AssemblyGrouping =
  "none" | "marketplace" | "warehouse" | "status" | "composition" | "type";

export const ASSEMBLY_GROUPING_LABELS: Record<AssemblyGrouping, string> = {
  none: "Без группировки",
  marketplace: "По магазинам",
  warehouse: "По складам",
  status: "По статусу",
  composition: "По составу",
  type: "По типу",
};

export function parseAssemblyGrouping(value: unknown): AssemblyGrouping {
  return typeof value === "string" && value in ASSEMBLY_GROUPING_LABELS
    ? (value as AssemblyGrouping)
    : "none";
}

export interface AssemblyOrderGroup {
  key: string;
  label: string;
  orders: OrderDetailsDto[];
}

interface GroupKey {
  key: string;
  label: string;
  // Groups sort by rank first, then by label.
  rank: number;
}

const STATUS_RANK: Record<AssemblyTaskStatus, number> = {pending: 0, inProgress: 1, done: 2};
const TYPE_RANK: Record<OrderType, number> = {fbs: 0, fbo: 1, direct: 2};

export function getOrderAssemblyStatus(order: OrderDetailsDto): AssemblyTaskStatus {
  const tasks = order.assemblyTasks;
  if (tasks.length > 0 && tasks.every((t) => t.status === "done")) return "done";
  if (tasks.every((t) => t.status === "pending")) return "pending";
  return "inProgress";
}

function getGroupKey(order: OrderDetailsDto, grouping: AssemblyGrouping): GroupKey {
  switch (grouping) {
    case "none":
      return {key: "", label: "", rank: 0};
    case "marketplace":
      return order.marketplaceOrder
        ? {
            key: order.marketplaceOrder.marketplaceAccountId,
            label: order.marketplaceOrder.marketplaceAccountName,
            rank: 0,
          }
        : {key: "", label: "Без магазина", rank: 1};
    case "warehouse":
      return {key: order.warehouseId, label: order.warehouseName, rank: 0};
    case "status": {
      const status = getOrderAssemblyStatus(order);
      return {key: status, label: TASK_STATUS_LABELS[status], rank: STATUS_RANK[status]};
    }
    case "type":
      return {key: order.type, label: ORDER_TYPE_LABELS[order.type], rank: TYPE_RANK[order.type]};
    case "composition":
      return getCompositionKey(order);
  }
}

function getCompositionKey(order: OrderDetailsDto): GroupKey {
  const items = new Map<string, {name: string; quantity: number}>();
  for (const box of order.boxes) {
    for (const c of box.components) {
      const item = items.get(c.catalogItemId);
      if (item) item.quantity += c.quantity;
      else items.set(c.catalogItemId, {name: c.catalogItemName, quantity: c.quantity});
    }
  }
  if (items.size === 0) return {key: "", label: "Пустой состав", rank: 1};

  const sorted = [...items].sort(([idA, a], [idB, b]) =>
    a.name === b.name ? idA.localeCompare(idB) : a.name.localeCompare(b.name),
  );
  return {
    key: sorted.map(([id, {quantity}]) => `${id}:${quantity}`).join("|"),
    label: sorted.map(([, {name, quantity}]) => `${name} ×${quantity}`).join(", "),
    rank: 0,
  };
}

export function groupAssemblyOrders(
  orders: OrderDetailsDto[],
  grouping: AssemblyGrouping,
): AssemblyOrderGroup[] {
  const groups = new Map<string, GroupKey & {orders: OrderDetailsDto[]}>();
  for (const order of orders) {
    const groupKey = getGroupKey(order, grouping);
    const group = groups.get(groupKey.key);
    if (group) group.orders.push(order);
    else groups.set(groupKey.key, {...groupKey, orders: [order]});
  }

  return [...groups.values()]
    .sort((a, b) => a.rank - b.rank || a.label.localeCompare(b.label, "ru"))
    .map(({key, label, orders}) => ({key, label, orders}));
}
