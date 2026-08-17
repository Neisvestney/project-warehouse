import type {StocktakeItemRequest, StocktakeNodeDto, StocktakeNodeStockDto} from "@/api/types.gen";

/**
 * One editable row of the counting screen. `expected` is live stock at the moment the cell was
 * opened — it is display-only, the server recomputes it when the document is finished.
 */
export interface DraftRow {
  key: string;
  kind: "standard" | "unit";
  catalogItemId: string;
  catalogItemName: string;
  expected: number;
  counted: number;
  inventoryNumber?: string;
  notes: string;
}

export function rowKey(kind: "standard" | "unit", catalogItemId: string, number?: string): string {
  return kind === "unit" ? `unit:${catalogItemId}:${number}` : `standard:${catalogItemId}`;
}

/**
 * Merges live stock with what was already counted. Untouched positions default to "matches the
 * books", so the operator only has to enter discrepancies. Saved lines with no live counterpart are
 * kept — those are surpluses entered earlier.
 */
export function buildDraftRows(node: StocktakeNodeDto, stock: StocktakeNodeStockDto): DraftRow[] {
  const savedByKey = new Map(
    node.items.map((item) => [
      rowKey(item.kind, item.catalogItemId, item.inventoryNumber ?? undefined),
      item,
    ]),
  );
  const rows: DraftRow[] = [];
  const usedKeys = new Set<string>();

  for (const entry of stock.standard) {
    const key = rowKey("standard", entry.catalogItemId);
    usedKeys.add(key);
    const saved = savedByKey.get(key);
    rows.push({
      key,
      kind: "standard",
      catalogItemId: entry.catalogItemId,
      catalogItemName: entry.catalogItemName,
      expected: entry.expected,
      counted: saved ? saved.countedQuantity : entry.expected,
      notes: saved?.notes ?? "",
    });
  }

  for (const entry of stock.units) {
    const key = rowKey("unit", entry.catalogItemId, entry.inventoryNumber);
    usedKeys.add(key);
    const saved = savedByKey.get(key);
    rows.push({
      key,
      kind: "unit",
      catalogItemId: entry.catalogItemId,
      catalogItemName: entry.catalogItemName,
      inventoryNumber: entry.inventoryNumber,
      expected: 1,
      counted: saved ? saved.countedQuantity : 1,
      notes: saved?.notes ?? "",
    });
  }

  for (const item of node.items) {
    const key = rowKey(item.kind, item.catalogItemId, item.inventoryNumber ?? undefined);
    if (usedKeys.has(key)) continue;
    rows.push({
      key,
      kind: item.kind,
      catalogItemId: item.catalogItemId,
      catalogItemName: item.catalogItemName,
      inventoryNumber: item.inventoryNumber ?? undefined,
      expected: 0,
      counted: item.countedQuantity,
      notes: item.notes ?? "",
    });
  }

  return rows;
}

export function draftToRequest(rows: DraftRow[]): StocktakeItemRequest[] {
  return rows.map((row) => ({
    kind: row.kind,
    catalogItemId: row.catalogItemId,
    countedQuantity: row.counted,
    inventoryNumber: row.kind === "unit" ? row.inventoryNumber : null,
    notes: row.notes || null,
  }));
}

export function hasDifferences(rows: DraftRow[]): boolean {
  return rows.some((row) => row.counted !== row.expected);
}
