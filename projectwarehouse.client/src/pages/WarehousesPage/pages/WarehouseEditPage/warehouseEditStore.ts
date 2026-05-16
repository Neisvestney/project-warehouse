import {makeAutoObservable} from "mobx";
import type {WarehouseDto, WarehouseLayoutObjectType} from "@/api/types.gen";
import {ObservableForm} from "@/components/ObservableForm";

export type Tool = "select" | "storagePlace" | "wall" | "passage";

export interface EditableStoragePlace {
  tempId: string;
  serverId: string | null;
  name: string;
  x: number;
  y: number;
  width: number;
  height: number;
  rotation: number;
}

export interface EditableLayoutObject {
  tempId: string;
  type: WarehouseLayoutObjectType;
  x: number;
  y: number;
  width: number;
  height: number;
  rotation: number;
}

export type EditableObject =
  | {kind: "storagePlace"; data: EditableStoragePlace}
  | {kind: "layoutObject"; data: EditableLayoutObject};

export interface WarehouseMetaFormValues {
  name: string;
  width: number;
  height: number;
}

export class WarehouseEditStore {
  storagePlaces: EditableStoragePlace[] = [];
  layoutObjects: EditableLayoutObject[] = [];
  selectedTempId: string | null = null;
  activeTool: Tool = "select";
  form = new ObservableForm<WarehouseMetaFormValues>();

  constructor() {
    makeAutoObservable(this);
  }

  get selectedObject(): EditableObject | null {
    if (!this.selectedTempId) return null;
    const sp = this.storagePlaces.find((s) => s.tempId === this.selectedTempId);
    if (sp) return {kind: "storagePlace", data: sp};
    const lo = this.layoutObjects.find((l) => l.tempId === this.selectedTempId);
    if (lo) return {kind: "layoutObject", data: lo};
    return null;
  }

  loadFromDto(warehouse: WarehouseDto): void {
    this.storagePlaces = warehouse.storagePlaces.map((sp) => ({
      tempId: sp.id,
      serverId: sp.id,
      name: sp.name,
      x: sp.x,
      y: sp.y,
      width: sp.width,
      height: sp.height,
      rotation: sp.rotation,
    }));
    this.layoutObjects = warehouse.layoutObjects.map((lo) => ({
      tempId: crypto.randomUUID(),
      type: lo.type,
      x: lo.x,
      y: lo.y,
      width: lo.width,
      height: lo.height,
      rotation: lo.rotation,
    }));
    this.form.data = {
      name: warehouse.name,
      width: warehouse.width,
      height: warehouse.height,
    };
  }

  selectObject(tempId: string | null): void {
    this.selectedTempId = tempId;
  }

  setActiveTool(tool: Tool): void {
    this.activeTool = tool;
    if (tool !== "select") this.selectedTempId = null;
  }

  addStoragePlace(data: Omit<EditableStoragePlace, "tempId" | "serverId">): void {
    this.storagePlaces.push({...data, tempId: crypto.randomUUID(), serverId: null});
  }

  addLayoutObject(data: Omit<EditableLayoutObject, "tempId">): void {
    this.layoutObjects.push({...data, tempId: crypto.randomUUID()});
  }

  updateStoragePlace(
    tempId: string,
    updates: Partial<Omit<EditableStoragePlace, "tempId" | "serverId">>,
  ): void {
    const sp = this.storagePlaces.find((s) => s.tempId === tempId);
    if (sp) Object.assign(sp, updates);
  }

  updateLayoutObject(tempId: string, updates: Partial<Omit<EditableLayoutObject, "tempId">>): void {
    const lo = this.layoutObjects.find((l) => l.tempId === tempId);
    if (lo) Object.assign(lo, updates);
  }

  deleteSelected(): void {
    if (!this.selectedTempId) return;
    const id = this.selectedTempId;
    this.storagePlaces = this.storagePlaces.filter((s) => s.tempId !== id);
    this.layoutObjects = this.layoutObjects.filter((l) => l.tempId !== id);
    this.selectedTempId = null;
  }

  toUpdateRequest() {
    const meta = this.form.data;
    return {
      name: meta?.name ?? "",
      width: meta?.width ?? 0,
      height: meta?.height ?? 0,
      storagePlaces: this.storagePlaces.map((sp) => ({
        id: sp.serverId ?? undefined,
        name: sp.name,
        x: sp.x,
        y: sp.y,
        width: sp.width,
        height: sp.height,
        rotation: sp.rotation,
      })),
      layoutObjects: this.layoutObjects.map((lo) => ({
        type: lo.type,
        x: lo.x,
        y: lo.y,
        width: lo.width,
        height: lo.height,
        rotation: lo.rotation,
      })),
    };
  }
}
