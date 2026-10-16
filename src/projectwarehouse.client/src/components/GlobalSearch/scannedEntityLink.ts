import {parseEntityBarcode} from "@/utils/barcodeUtils";
import {entitiesTypes} from "@/utils/appEntityUtils";
import {interpolateArgs} from "@/utils/interpolateArgs";

/** Link of the entity an app-printed barcode points at, or null when the code is foreign or has no page. */
export function getScannedEntityLink(raw: string): string | null {
  const parsed = parseEntityBarcode(raw);
  if (!parsed) return null;
  const template = entitiesTypes[parsed.entity].linkTemplate;
  if (template === "no-link" || template === "#") return null;
  return interpolateArgs(template, {id: parsed.id});
}
