import type {AssemblyTaskDto, CatalogItemType} from "@/api/types.gen";
import {getRemainingQty} from "./batchEligibility";

export interface SelectedTaskInfo {
  orderId: string;
  taskId: string;
  task: AssemblyTaskDto;
  warehouseId: string;
  orderNumber?: string;
  postingNumber?: string | null;
}

export interface BatchTarget {
  orderId: string;
  taskId: string;
  taskBoxId: string;
  componentId: string;
  qty: number;
  orderNumber?: string;
  postingNumber?: string | null;
}

export interface BatchGroup {
  key: string;
  catalogItemId: string;
  catalogItemName: string;
  catalogItemType: CatalogItemType;
  warehouseId: string;
  totalNeeded: number;
  targets: BatchTarget[];
}

export function buildBatchGroups(selectedTasks: SelectedTaskInfo[]): BatchGroup[] {
  const groupMap = new Map<string, BatchGroup>();

  for (const {orderId, taskId, task, warehouseId, orderNumber, postingNumber} of selectedTasks) {
    for (const box of task.boxes) {
      for (const comp of box.components) {
        const remaining = getRemainingQty(comp);
        if (remaining <= 0) continue;

        const key = `${comp.catalogItemId}::${warehouseId}`;
        const target: BatchTarget = {
          orderId,
          taskId,
          taskBoxId: box.id,
          componentId: comp.id,
          qty: remaining,
          orderNumber,
          postingNumber,
        };
        const existing = groupMap.get(key);
        if (existing) {
          existing.totalNeeded += remaining;
          existing.targets.push(target);
        } else {
          groupMap.set(key, {
            key,
            catalogItemId: comp.catalogItemId,
            catalogItemName: comp.catalogItemName,
            catalogItemType: comp.catalogItemType,
            warehouseId,
            totalNeeded: remaining,
            targets: [target],
          });
        }
      }
    }
  }

  return Array.from(groupMap.values());
}

export const NO_EXCLUDED_TASKS: ReadonlySet<string> = new Set();

/** One line per task of the group, which is what the exclusion picker lists and excludes by. */
export interface GroupTaskEntry {
  taskId: string;
  orderNumber?: string;
  postingNumber?: string | null;
  qty: number;
}

export function groupTaskEntries(group: BatchGroup): GroupTaskEntry[] {
  const byTask = new Map<string, GroupTaskEntry>();
  for (const target of group.targets) {
    const entry = byTask.get(target.taskId);
    if (entry) {
      entry.qty += target.qty;
    } else {
      byTask.set(target.taskId, {
        taskId: target.taskId,
        orderNumber: target.orderNumber,
        postingNumber: target.postingNumber,
        qty: target.qty,
      });
    }
  }
  return Array.from(byTask.values());
}

/**
 * The group as the batch actually sees it: excluded tasks are gone from `targets`, and `totalNeeded`
 * shrinks with them, so every consumer — the stock check, the copy count, the request — follows.
 */
export function withoutExcludedTasks(group: BatchGroup, excludedTaskIds: ReadonlySet<string>) {
  if (excludedTaskIds.size === 0) return group;
  const targets = group.targets.filter((t) => !excludedTaskIds.has(t.taskId));
  if (targets.length === group.targets.length) return group;
  return {
    ...group,
    targets,
    totalNeeded: targets.reduce((sum, t) => sum + t.qty, 0),
  };
}
