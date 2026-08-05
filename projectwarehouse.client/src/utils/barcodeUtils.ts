const BARCODE_NAMESPACE = "pw";

const ENTITY_CODES = {
  storagePlaceNode: "spn",
  catalogItem: "ci",
} as const;

export type BarcodeEntity = keyof typeof ENTITY_CODES;

export interface ParsedBarcode {
  entity: BarcodeEntity;
  id: string;
}

export function formatEntityBarcode(entity: BarcodeEntity, id: string): string {
  return `${BARCODE_NAMESPACE}:${ENTITY_CODES[entity]}:${id}`;
}

export function parseEntityBarcode(raw: string): ParsedBarcode | null {
  const parts = raw.trim().split(":");
  if (parts.length !== 3) return null;

  const [namespace, code, id] = parts;
  if (namespace.toLowerCase() !== BARCODE_NAMESPACE || !id) return null;

  const entity = (Object.keys(ENTITY_CODES) as BarcodeEntity[]).find(
    (key) => ENTITY_CODES[key] === code.toLowerCase(),
  );
  return entity ? {entity, id} : null;
}
