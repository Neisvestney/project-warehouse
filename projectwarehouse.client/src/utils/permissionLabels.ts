import type {PermissionName} from "@/api";

const permissionLabels: Record<PermissionName, string> = {
  "users.view": "Просмотр пользователей",
  "users.create": "Создание пользователей",
  "users.edit_profile": "Редактирование профиля",
  "users.delete": "Удаление пользователей",
  "users.manage_roles_and_permissions": "Управление ролями и правами",
  "users.manage_assigned_warehouses": "Управление назначенными складами",
  "users.reset_password": "Сброс пароля",
  "roles.view": "Просмотр ролей",
  "roles.edit": "Редактирование ролей",
  "catalog.view": "Просмотр каталога",
  "catalog.edit": "Редактирование каталога",
  "warehouses.view": "Просмотр всех складов",
  "warehouses.edit": "Редактирование всех складов",
  "warehouses.view_assigned": "Просмотр назначенных складов",
  "warehouses.edit_assigned": "Редактирование назначенных складов",
  "changelog.view": "Просмотр списка изменений",
  "receipts.view": "Просмотр всех приемок",
  "receipts.edit": "Редактирование всех приемок",
  "receipts.view_assigned": "Просмотр приемок назначенных складов",
  "receipts.edit_assigned": "Редактирование приемок назначенных складов",
  "receipts.process_assigned": "Обработка приемок назначенных складов",
  "transfers.execute": "Перемещение товаров (все склады)",
  "transfers.execute_assigned": "Перемещение товаров (назначенные склады)",
};

export function getPermissionLabel(permission: PermissionName | string): string {
  return permissionLabels[permission as PermissionName] ?? permission;
}
