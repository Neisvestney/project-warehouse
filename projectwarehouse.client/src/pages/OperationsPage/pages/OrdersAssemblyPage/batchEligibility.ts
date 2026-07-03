import type {AssemblyTaskDto} from "@/api/types.gen";

export function checkBatchEligibility(task: AssemblyTaskDto): boolean {
  return task.boxes.every((box) =>
    box.components.every((c) => isComponentEligible(c.catalogItemType)),
  );
}

function isComponentEligible(type: string): boolean {
  return type === "standard" || type === "variation" || type === "bundle";
}
