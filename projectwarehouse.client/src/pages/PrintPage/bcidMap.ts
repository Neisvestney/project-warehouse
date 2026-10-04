import type {BarcodeType} from "./BarcodeLabel.tsx";

export const BCID_MAP: Record<BarcodeType, string> = {
  DataMatrix: "datamatrix",
  EAN13: "ean13",
  Code128: "code128",
  QR: "qrcode",
};
