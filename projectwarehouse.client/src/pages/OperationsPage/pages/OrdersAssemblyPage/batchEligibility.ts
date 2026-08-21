import type {AssemblyTaskBoxComponentDto, AssemblyTaskDto} from "@/api/types.gen";
import {countFulfilledQty} from "@/components/orders/orderAssemblyUtils";

export function checkBatchEligibility(task: AssemblyTaskDto): boolean {
  return task.boxes.every((box) => box.components.every((c) => isComponentEligible(c)));
}

/** Remaining quantity to fulfill for a component — batch assembly operates on this, not on the nominal quantity. */
export function getRemainingQty(component: AssemblyTaskBoxComponentDto): number {
  return component.quantity - countFulfilledQty(component.fulfillments);
}

export function hasRemainingWork(task: AssemblyTaskDto): boolean {
  return task.boxes.some((box) => box.components.some((c) => getRemainingQty(c) > 0));
}

/** Empty string means the task can go into batch assembly; otherwise it is the tooltip text. */
export function getBatchDisabledReason(task: AssemblyTaskDto, eligible: boolean): string {
  if (!eligible) return "Содержит Единичные товары";
  if (!hasRemainingWork(task)) return "Задание полностью собрано";
  return "";
}

function isComponentEligible(component: AssemblyTaskBoxComponentDto): boolean {
  return !component.containsUnit;
}
