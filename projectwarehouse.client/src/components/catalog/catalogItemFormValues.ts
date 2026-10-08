import type {CatalogItemSelectDto, CatalogItemTagDto, DataFileDto} from "@/api/types.gen";
import {normalizeGtin} from "@/utils/gtinUtils";

export type ComponentValue = {
  entityId?: string;
  component: CatalogItemSelectDto | null;
  quantity: number;
};

export type ImageValue = {
  /** Id of the join row, absent until the image is saved with the item. */
  entityId?: string;
  file: DataFileDto;
};

export type ChildValue = {
  entityId?: string;
  type: "standard" | "unit";
  name: string;
  article: string;
  barcode: string;
  gtin: string;
  description: string;
  notes: string;
  labelText: string;
  tags: CatalogItemTagDto[];
  mainImage: DataFileDto | null;
  images: ImageValue[];
};

// unknown: the field components type `rules` over every path of the form
export const validateGtinField = (value: unknown) =>
  typeof value !== "string" ||
  !value.trim() ||
  normalizeGtin(value) !== null ||
  "Некорректный GTIN";

export type CatalogItemFormValues = {
  name: string;
  article: string;
  barcode: string;
  gtin: string;
  description: string;
  notes: string;
  labelText: string;
  isArchived: boolean;
  tags: CatalogItemTagDto[];
  members: CatalogItemSelectDto[];
  components: ComponentValue[];
  children: ChildValue[];
  mainImage: DataFileDto | null;
  images: ImageValue[];
};
