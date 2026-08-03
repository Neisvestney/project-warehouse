import type {AssemblyTaskBoxComponentDto, AssemblyTaskDto} from "@/api/types.gen";

export function checkBatchEligibility(task: AssemblyTaskDto): boolean {
  return task.boxes.every((box) => box.components.every((c) => isComponentEligible(c)));
}

function isComponentEligible(component: AssemblyTaskBoxComponentDto): boolean {
  return !component.containsUnit;
}
