import {useEffect, useLayoutEffect, useRef, useState} from "react";
import Box from "@mui/material/Box";
import Typography from "@mui/material/Typography";
import * as bwipjs from "bwip-js/browser";
import type {BarcodeType} from "./BarcodeLabel.tsx";
import {BCID_MAP} from "./bcidMap.ts";

const CODE_SIZE_RATIO = 0.35;
const LINE_HEIGHT = 1.2;
const ELLIPSIS = "…";
// absorbs sub-pixel rounding between scrollHeight and clientHeight on mm-sized boxes
const OVERFLOW_TOLERANCE_PX = 1;

interface TextLabelProps {
  type: BarcodeType;
  value: string;
  label?: string;
  widthMm: number;
  heightMm: number;
  paddingMm: number;
  fontSizePx: number;
}

function TextLabel({type, value, label, widthMm, heightMm, paddingMm, fontSizePx}: TextLabelProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const boxRef = useRef<HTMLDivElement>(null);
  const titleRef = useRef<HTMLParagraphElement>(null);
  const [error, setError] = useState<string | null>(null);

  const [title, ...rest] = (label ?? value).split("\n");
  const subtitle = rest.join("\n");
  const innerWidthMm = Math.max(1, widthMm - 2 * paddingMm);
  const innerHeightMm = Math.max(1, heightMm - 2 * paddingMm);
  const codeSizeMm = Math.max(1, Math.min(innerWidthMm, innerHeightMm) * CODE_SIZE_RATIO);

  // bumped when a web font finishes loading: a measurement taken on the fallback font is stale
  const [fontsVersion, setFontsVersion] = useState(0);
  useEffect(() => {
    const fonts = document.fonts;
    const bump = () => setFontsVersion((v) => v + 1);
    fonts.addEventListener("loadingdone", bump);
    void fonts.ready.then(bump);
    return () => fonts.removeEventListener("loadingdone", bump);
  }, []);

  const fitKey = [title, subtitle, widthMm, heightMm, paddingMm, fontSizePx, fontsVersion].join(
    "|",
  );
  const [fit, setFit] = useState<{key: string; text: string} | null>(null);
  const shownTitle = fit?.key === fitKey ? fit.text : title;

  // Text wrapping around a float rules out line-clamp, so the title is shortened by measuring until the subtitle fits.
  useLayoutEffect(() => {
    const box = boxRef.current;
    const node = titleRef.current?.firstChild;
    if (!box || !(node instanceof Text)) return;
    const rendered = node.nodeValue;
    const fitsWith = (text: string) => {
      node.nodeValue = text;
      return box.scrollHeight - box.clientHeight <= OVERFLOW_TOLERANCE_PX;
    };
    let best = title;
    if (!fitsWith(title)) {
      let lo = 0;
      let hi = title.length - 1;
      while (lo < hi) {
        const mid = Math.ceil((lo + hi) / 2);
        if (fitsWith(title.slice(0, mid).trimEnd() + ELLIPSIS)) lo = mid;
        else hi = mid - 1;
      }
      best = title.slice(0, lo).trimEnd() + ELLIPSIS;
    }
    node.nodeValue = rendered;
    setFit({key: fitKey, text: best});
  }, [fitKey, title]);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    let renderError: string | null = null;
    try {
      bwipjs.toCanvas(canvas, {bcid: BCID_MAP[type], text: value, scale: 4, includetext: false});
    } catch (e) {
      renderError = e instanceof Error ? e.message : "Ошибка генерации";
      const ctx = canvas.getContext("2d");
      if (ctx) ctx.clearRect(0, 0, canvas.width, canvas.height);
    }
    setError(renderError);
  }, [type, value]);

  return (
    <Box
      sx={{
        width: `${widthMm}mm`,
        height: `${heightMm}mm`,
        overflow: "hidden",
        border: "1px dashed",
        borderColor: "divider",
        p: `${paddingMm}mm`,
        boxSizing: "border-box",
        "@media print": {border: "none", p: `${paddingMm}mm`},
      }}
    >
      <Box ref={boxRef} sx={{height: "100%", overflow: "hidden"}}>
        <Box sx={{float: "right", width: 0, height: `calc(100% - ${codeSizeMm}mm)`}} />
        <Box
          sx={{
            float: "right",
            clear: "right",
            width: `${codeSizeMm}mm`,
            height: `${codeSizeMm}mm`,
            ml: "1mm",
          }}
        >
          {error ? (
            <Typography variant="caption" color="error" sx={{fontSize: "8px"}}>
              {error}
            </Typography>
          ) : (
            <canvas
              ref={canvasRef}
              style={{
                display: "block",
                width: "100%",
                height: "100%",
                objectFit: "contain",
                imageRendering: "pixelated",
              }}
            />
          )}
        </Box>
        <Typography
          ref={titleRef}
          sx={{
            fontSize: `${fontSizePx * 1.5}px`,
            fontWeight: 700,
            lineHeight: LINE_HEIGHT,
            wordBreak: "break-word",
          }}
        >
          {shownTitle}
        </Typography>
        {subtitle && (
          <Typography
            sx={{
              fontSize: `${fontSizePx * 1.25}px`,
              lineHeight: LINE_HEIGHT,
              whiteSpace: "pre-line",
              wordBreak: "break-word",
              mt: "0.5mm",
            }}
          >
            {subtitle}
          </Typography>
        )}
      </Box>
    </Box>
  );
}

export default TextLabel;
