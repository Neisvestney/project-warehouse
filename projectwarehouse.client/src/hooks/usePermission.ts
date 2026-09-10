import type {PermissionName} from "@/api/types.gen";
import {hasPermission} from "@/utils/permissions";
import {useAuth} from "./useAuth";

export function useHasPermission(
  permission: PermissionName | PermissionName[] | undefined,
  mode: "any" | "all" = "any",
): boolean {
  const {user} = useAuth();
  return hasPermission(user?.permissions ?? [], permission, mode);
}

/**
 * Whether the warehouse is in the user's assignment. `undefined` — nothing loaded yet — reads as "no",
 * so scoped UI stays closed instead of flashing.
 */
export function useIsAssignedToWarehouse(warehouseId: string | undefined): boolean {
  const {user} = useAuth();
  if (warehouseId === undefined) return false;
  return user?.assignedWarehouseIds.includes(warehouseId) ?? false;
}

/**
 * Warehouse-scoped variant: the unscoped permission passes everywhere, the `_assigned` one only for a
 * warehouse the user is actually assigned to — the same pair the server's WarehouseScopedRule checks.
 */
export function useHasWarehousePermission(
  all: PermissionName | PermissionName[],
  assigned: PermissionName | PermissionName[],
  warehouseId: string | undefined,
): boolean {
  const granted = useAuth().user?.permissions ?? [];
  const isAssigned = useIsAssignedToWarehouse(warehouseId);

  if (hasPermission(granted, all)) return true;
  return hasPermission(granted, assigned) && isAssigned;
}
