import type {AssemblyFulfillmentDto, AssemblyTaskDto} from "@/api/types.gen";

export function countFulfilledQty(fulfillments: AssemblyFulfillmentDto[]): number {
  return fulfillments.reduce((sum, f) => {
    if (f.unitInventoryItemId || f.assembledBundleInventoryItemId || f.bundleComponents?.length)
      return sum + 1;
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
