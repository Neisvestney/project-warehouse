import {useEffect, useRef, useState} from "react";
import Box from "@mui/material/Box";
import Typography from "@mui/material/Typography";
import * as bwipjs from "bwip-js/browser";

export type BarcodeType = "DataMatrix" | "EAN13" | "Code128" | "QR";

const BCID_MAP: Record<BarcodeType, string> = {
  DataMatrix: "datamatrix",
  EAN13: "ean13",
  Code128: "code128",
  QR: "qrcode",
};

const IS_1D: Record<BarcodeType, boolean> = {
  DataMatrix: false,
  EAN13: true,
  Code128: true,
  QR: false,
};

// bwip-js height unit ≈ 1pt = 0.3527mm at default 72dpi
const MM_PER_BWIP_HEIGHT_UNIT = 0.3527;

interface BarcodeLabelProps {
  type: BarcodeType;
  value: string;
  label?: string;
  widthMm: number;
  heightMm: number;
  paddingMm: number;
}

function BarcodeLabel({type, value, label, widthMm, heightMm, paddingMm}: BarcodeLabelProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [error, setError] = useState<string | null>(null);

  const captionHeightMm = 5;
  const labelHeightMm = label ? 5 : 0;
  const bwipHeightMm = heightMm - captionHeightMm - labelHeightMm;
  const canvasHeightMm = Math.max(1, heightMm - 2 * paddingMm - captionHeightMm - labelHeightMm);
  const bwipHeight = IS_1D[type]
    ? Math.max(1, Math.round(bwipHeightMm / MM_PER_BWIP_HEIGHT_UNIT))
    : undefined;

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    let renderError: string | null = null;
    try {
      bwipjs.toCanvas(canvas, {
        bcid: BCID_MAP[type],
        text: value,
        scale: 4,
        includetext: false,
        ...(bwipHeight !== undefined && {height: bwipHeight}),
      });
    } catch (e) {
      renderError = e instanceof Error ? e.message : "Ошибка генерации";
      const ctx = canvas.getContext("2d");
      if (ctx) ctx.clearRect(0, 0, canvas.width, canvas.height);
    }
    setError(renderError);
  }, [type, value, bwipHeight]);

  return (
    <Box
      sx={{
        width: `${widthMm}mm`,
        height: `${heightMm}mm`,
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        overflow: "hidden",
        border: "1px dashed",
        borderColor: "divider",
        p: `${paddingMm}mm`,
        boxSizing: "border-box",
        "@media print": {border: "none", p: `${paddingMm}mm`},
      }}
    >
      {label && (
        <Typography
          variant="caption"
          sx={{
            fontSize: "8px",
            fontWeight: 600,
            lineHeight: 1.2,
            textAlign: "center",
            wordBreak: "break-word",
            flexShrink: 0,
            mb: "0.5mm",
          }}
        >
          {label}
        </Typography>
      )}
      {error ? (
        <Typography variant="caption" color="error" sx={{textAlign: "center", fontSize: "8px"}}>
          {error}
        </Typography>
      ) : (
        <canvas
          ref={canvasRef}
          style={
            IS_1D[type]
              ? {width: "100%", maxHeight: `${canvasHeightMm}mm`}
              : {maxWidth: "100%", maxHeight: `${canvasHeightMm}mm`, objectFit: "contain"}
          }
        />
      )}
      <Typography
        variant="caption"
        sx={{
          fontSize: "7px",
          lineHeight: 1.2,
          textAlign: "center",
          wordBreak: "break-all",
          mt: "0.5mm",
          flexShrink: 0,
        }}
      >
        {value}
      </Typography>
    </Box>
  );
}

export default BarcodeLabel;
