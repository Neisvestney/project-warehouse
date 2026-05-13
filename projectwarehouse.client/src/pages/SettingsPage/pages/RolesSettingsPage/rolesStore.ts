import {makeAutoObservable} from "mobx";
import {arrayMove} from "@dnd-kit/sortable";
import type {RoleWithPermissionsDto, UpdateRoleItem} from "@/api/types.gen";

export class EditableRole {
  tempId: string;
  serverId: string | null;
  name: string;
  permissions: string[];

  constructor(data: {
    tempId: string;
    serverId: string | null;
    name: string;
    permissions: string[];
  }) {
    this.tempId = data.tempId;
    this.serverId = data.serverId;
    this.name = data.name;
    this.permissions = data.permissions;
    makeAutoObservable(this);
  }

  hasPermission(permission: string): boolean {
    return this.permissions.includes(permission);
  }

  togglePermission(permission: string): void {
    const idx = this.permissions.indexOf(permission);
    if (idx >= 0) {
      this.permissions.splice(idx, 1);
    } else {
      this.permissions.push(permission);
    }
  }

  rename(name: string): void {
    this.name = name;
  }
}

function snapshot(roles: EditableRole[]): string {
  return JSON.stringify(
    roles.map((r) => ({
      serverId: r.serverId,
      name: r.name,
      permissions: r.permissions.slice().sort(),
    })),
  );
}

export class RolesStore {
  roles: EditableRole[] = [];
  allPermissions: string[] = [];
  originalJson = "";

  constructor() {
    makeAutoObservable(this);
  }

  get hasData(): boolean {
    return this.allPermissions.length > 0;
  }

  get isDirty(): boolean {
    return snapshot(this.roles) !== this.originalJson;
  }

  get isValid(): boolean {
    return this.roles.every((r) => r.name.trim() !== "");
  }

  loadData(roles: RoleWithPermissionsDto[], permissions: string[]): void {
    const sorted = roles.slice().sort((a, b) => a.order - b.order);
    this.roles = sorted.map(
      (r) =>
        new EditableRole({
          tempId: r.id,
          serverId: r.id,
          name: r.name,
          permissions: r.permissions.slice(),
        }),
    );
    this.allPermissions = permissions;
    this.originalJson = snapshot(this.roles);
  }

  syncRolesFromServer(roles: RoleWithPermissionsDto[]): void {
    const sorted = roles.slice().sort((a, b) => a.order - b.order);
    this.roles = sorted.map(
      (r) =>
        new EditableRole({
          tempId: r.id,
          serverId: r.id,
          name: r.name,
          permissions: r.permissions.slice(),
        }),
    );
    this.originalJson = snapshot(this.roles);
  }

  reset(): void {
    const parsed = JSON.parse(this.originalJson) as Array<{
      serverId: string | null;
      name: string;
      permissions: string[];
    }>;
    this.roles = parsed.map(
      (r) =>
        new EditableRole({
          tempId: r.serverId ?? crypto.randomUUID(),
          serverId: r.serverId,
          name: r.name,
          permissions: r.permissions.slice(),
        }),
    );
  }

  addRole(): void {
    this.roles = [
      ...this.roles,
      new EditableRole({
        tempId: crypto.randomUUID(),
        serverId: null,
        name: "",
        permissions: [],
      }),
    ];
  }

  removeRole(tempId: string): void {
    this.roles = this.roles.filter((r) => r.tempId !== tempId);
  }

  renameRole(tempId: string, name: string): void {
    this.roles.find((r) => r.tempId === tempId)?.rename(name);
  }

  togglePermission(tempId: string, permission: string): void {
    this.roles.find((r) => r.tempId === tempId)?.togglePermission(permission);
  }

  reorderRoles(activeId: string, overId: string): void {
    const activeIndex = this.roles.findIndex((r) => r.tempId === activeId);
    const overIndex = this.roles.findIndex((r) => r.tempId === overId);
    if (activeIndex !== -1 && overIndex !== -1) {
      this.roles = arrayMove(this.roles, activeIndex, overIndex);
    }
  }

  toUpdatePayload(): UpdateRoleItem[] {
    return this.roles.map((r, index) => ({
      id: r.serverId ?? undefined,
      name: r.name,
      order: index,
      permissions: r.permissions.slice(),
    }));
  }
}
