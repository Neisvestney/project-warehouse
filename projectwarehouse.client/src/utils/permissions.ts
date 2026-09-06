import type {PermissionName} from "@/api/types.gen";

// An absent or empty requirement means "no permission needed", not "nobody allowed".
export function hasPermission(
  granted: readonly string[],
  required: PermissionName | PermissionName[] | undefined,
  mode: "any" | "all" = "any",
): boolean {
  if (!required) return true;
  const list = Array.isArray(required) ? required : [required];
  if (list.length === 0) return true;
  return mode === "all"
    ? list.every((p) => granted.includes(p))
    : list.some((p) => granted.includes(p));
}
