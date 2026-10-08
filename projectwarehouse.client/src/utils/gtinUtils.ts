const GS = "\u001d";
// AIM symbology identifier some scanners prepend: ]d2 DataMatrix, ]C1 GS1-128, ]Q3 GS1 QR, ]e0 DataBar
const SYMBOLOGY_PREFIX = /^\][A-Za-z]\d/;
const MARK_GTIN = /^01(\d{14})/;
const HUMAN_READABLE_MARK_GTIN = /^\(01\)(\d{14})/;

/** Pads a GTIN-8/12/13/14 to 14 digits; null when the length or check digit is wrong. */
export function normalizeGtin(raw: string): string | null {
  const digits = raw.trim();
  if (!/^\d+$/.test(digits) || ![8, 12, 13, 14].includes(digits.length)) return null;
  const gtin = digits.padStart(14, "0");
  return hasValidCheckDigit(gtin) ? gtin : null;
}

function hasValidCheckDigit(gtin14: string): boolean {
  let sum = 0;
  for (let i = 0; i < 13; i++) sum += Number(gtin14[i]) * (i % 2 === 0 ? 3 : 1);
  return (10 - (sum % 10)) % 10 === Number(gtin14[13]);
}

/** GTIN (AI 01) of a GS1 element string such as a Честный знак mark; null for any other code. */
export function extractMarkGtin(raw: string): string | null {
  let code = raw.trim().replace(SYMBOLOGY_PREFIX, "");
  if (code.startsWith(GS)) code = code.slice(1);
  const match = MARK_GTIN.exec(code) ?? HUMAN_READABLE_MARK_GTIN.exec(code);
  return match && hasValidCheckDigit(match[1]) ? match[1] : null;
}

/** GTIN from either a Честный знак mark or a plain EAN/UPC barcode. */
export function gtinFromScan(raw: string): string {
  return extractMarkGtin(raw) ?? normalizeGtin(raw) ?? raw.trim();
}

/** Barcode for the item's barcode field: a mark yields the EAN-13 it carries, anything else stays as scanned. */
export function barcodeFromScan(raw: string): string {
  const gtin = extractMarkGtin(raw);
  if (!gtin) return raw.trim();
  return gtin.startsWith("0") ? gtin.slice(1) : gtin;
}
