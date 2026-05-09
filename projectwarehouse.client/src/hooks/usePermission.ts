import type { PermissionName } from '@/api/types.gen';
import { useAuth } from './useAuth';

export function useHasPermission(
  permission: PermissionName | PermissionName[],
  mode: 'any' | 'all' = 'any',
): boolean {
  const { user } = useAuth();
  if (!user) return false;
  const required = Array.isArray(permission) ? permission : [permission];
  return mode === 'all'
    ? required.every((p) => user.permissions.includes(p))
    : required.some((p) => user.permissions.includes(p));
}
