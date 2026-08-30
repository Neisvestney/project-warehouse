import type {AssemblyFulfillmentDto, AssemblyTaskDto, OrderDetailsDto} from "@/api/types.gen";

export type FulfillmentKind = "unit" | "bundle" | "standard";

export function getFulfillmentKind(fulfillment: AssemblyFulfillmentDto): FulfillmentKind {
  if (fulfillment.unitInventoryItemId) return "unit";
  if (fulfillment.bundleComponents?.length) return "bundle";
  return "standard";
}

/** Fulfillments live on assembly tasks, so an order box component's ones are spread across every
 * task that took on that box. */
export function collectBoxComponentFulfillments(
  order: OrderDetailsDto,
  orderBoxId: string,
  catalogItemId: string,
): AssemblyFulfillmentDto[] {
  return order.assemblyTasks.flatMap((task) =>
    task.boxes
      .filter((box) => box.orderBoxId === orderBoxId)
      .flatMap((box) =>
        box.components
          .filter((c) => c.catalogItemId === catalogItemId)
          .flatMap((c) => c.fulfillments),
      ),
  );
}

export function countFulfilledQty(fulfillments: AssemblyFulfillmentDto[]): number {
  return fulfillments.reduce((sum, f) => {
    if (f.unitInventoryItemId || f.bundleComponents?.length) return sum + 1;
    return sum + f.quantity;
  }, 0);
}

export function getTaskProgress(task: AssemblyTaskDto): {fulfilled: number; total: number} {
  let total = 0;
  let fulfilled = 0;
  for (const box of task.boxes) {
    for (const c of box.components) {
      total++;
      if (countFulfilledQty(c.fulfillments) >= c.quantity) fulfilled++;
    }
  }
  return {fulfilled, total};
}

/** Mirrors the backend's IsOrderFullyFulfilledAsync: every component of every task must be fully
 * picked, not just marked Done. Used to gate the manual "Собрать" recovery action. */
export function isOrderFullyFulfilled(order: OrderDetailsDto): boolean {
  const components = order.assemblyTasks.flatMap((t) => t.boxes.flatMap((b) => b.components));
  return (
    components.length > 0 &&
    components.every((c) => countFulfilledQty(c.fulfillments) >= c.quantity)
  );
}
