const permissionLabels: Record<string, string> = {
  "users.view": "Просмотр пользователей",
  "users.create": "Создание пользователей",
  "users.edit_profile": "Редактирование профиля",
  "users.delete": "Удаление пользователей",
  "users.manage_roles_and_permissions": "Управление ролями и правами",
  "users.reset_password": "Сброс пароля",
  "roles.view": "Просмотр ролей",
  "roles.edit": "Редактирование ролей",
};

export function getPermissionLabel(permission: string): string {
  return permissionLabels[permission] ?? permission;
}
