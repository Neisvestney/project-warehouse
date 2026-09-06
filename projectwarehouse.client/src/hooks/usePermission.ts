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
