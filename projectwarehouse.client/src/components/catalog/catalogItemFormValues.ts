import type {CatalogItemSelectDto, CatalogItemTagDto, DataFileDto} from "@/api/types.gen";

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
  description: string;
  notes: string;
  labelText: string;
  tags: CatalogItemTagDto[];
  mainImage: DataFileDto | null;
  images: ImageValue[];
};

export type CatalogItemFormValues = {
  name: string;
  article: string;
  barcode: string;
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
