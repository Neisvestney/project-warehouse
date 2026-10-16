import type {AppEntityType} from "@/api/types.gen";

/** Identity of a watched object across the registry, the presence map and the held-lock set. */
export const watchKey = (entityType: AppEntityType, entityId: string) =>
  `${entityType}:${entityId}`;
