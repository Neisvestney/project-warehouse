import React, {useRef} from "react";
import type {NormalizedBarcodePosition} from "../../../utils/qrTools.ts";

export interface ViewfinderRect {
  x: number;
  y: number;
  w: number;
  h: number;
}

interface ScanFrameOverlayProps {
  containerRef: React.RefObject<HTMLDivElement | null>;
  videoRef: React.RefObject<HTMLVideoElement | null>;
  viewfinderRect: ViewfinderRect;
  detectedPositions: NormalizedBarcodePosition[];
  onResize?: (newW: number, newH: number) => void;
}

const BRACKET_RATIO = 0.22;
const HANDLE_RADIUS = 22;

/** Перевод натуральных координат видео в экранные пиксели (с учётом object-fit: cover) */
function naturalToScreen(
  pt: {x: number; y: number},
  vw: number,
  vh: number,
  dw: number,
  dh: number,
): {x: number; y: number} {
  const scale = Math.max(dw / vw, dh / vh);
  const ox = (vw * scale - dw) / 2;
  const oy = (vh * scale - dh) / 2;
  return {x: pt.x * scale - ox, y: pt.y * scale - oy};
}

interface DragState {
  pointerId: number;
  startX: number;
  startY: number;
  initialW: number;
  initialH: number;
  dxSign: number;
  dySign: number;
}

export const ScanFrameOverlay: React.FC<ScanFrameOverlayProps> = ({
  containerRef,
  videoRef,
  viewfinderRect: vf,
  detectedPositions,
  onResize,
}) => {
  const video = videoRef.current;
  const container = containerRef.current;
  const vw = video?.videoWidth ?? 0;
  const vh = video?.videoHeight ?? 0;
  const dw = container?.clientWidth ?? 0;
  const dh = container?.clientHeight ?? 0;
  const hasVideoSize = vw > 0 && vh > 0 && dw > 0 && dh > 0;

  const dragRef = useRef<DragState | null>(null);

  const barcodePolygons = hasVideoSize
    ? detectedPositions.map((pos) => {
        const tl = naturalToScreen(pos.topLeft, vw, vh, dw, dh);
        const tr = naturalToScreen(pos.topRight, vw, vh, dw, dh);
        const br = naturalToScreen(pos.bottomRight, vw, vh, dw, dh);
        const bl = naturalToScreen(pos.bottomLeft, vw, vh, dw, dh);
        return {
          points: `${tl.x},${tl.y} ${tr.x},${tr.y} ${br.x},${br.y} ${bl.x},${bl.y}`,
          inViewfinder: pos.inViewfinder,
        };
      })
    : [];

  const bracketLen = BRACKET_RATIO * Math.min(vf.w, vf.h);
  const {x, y, w, h} = vf;

  const corners = [
    {id: "nw", cx: x, cy: y, cursor: "nw-resize", dxSign: -1, dySign: -1},
    {id: "ne", cx: x + w, cy: y, cursor: "ne-resize", dxSign: 1, dySign: -1},
    {id: "sw", cx: x, cy: y + h, cursor: "sw-resize", dxSign: -1, dySign: 1},
    {id: "se", cx: x + w, cy: y + h, cursor: "se-resize", dxSign: 1, dySign: 1},
  ];

  const handlePointerDown = (
    e: React.PointerEvent<SVGCircleElement>,
    dxSign: number,
    dySign: number,
  ) => {
    if (!onResize) return;
    e.stopPropagation();
    e.preventDefault();
    e.currentTarget.setPointerCapture(e.pointerId);
    dragRef.current = {
      pointerId: e.pointerId,
      startX: e.clientX,
      startY: e.clientY,
      initialW: vf.w,
      initialH: vf.h,
      dxSign,
      dySign,
    };
  };

  const handlePointerMove = (e: React.PointerEvent<SVGCircleElement>) => {
    const drag = dragRef.current;
    if (!drag || drag.pointerId !== e.pointerId || !onResize) return;
    const rawDX = e.clientX - drag.startX;
    const rawDY = e.clientY - drag.startY;
    const newW = drag.initialW + 2 * rawDX * drag.dxSign;
    const newH = drag.initialH + 2 * rawDY * drag.dySign;
    onResize(newW, newH);
  };

  const handlePointerUp = (e: React.PointerEvent<SVGCircleElement>) => {
    if (dragRef.current?.pointerId === e.pointerId) {
      dragRef.current = null;
    }
  };

  return (
    <div
      style={{
        position: "absolute",
        inset: 0,
        overflow: "hidden",
        pointerEvents: "none",
        touchAction: "none",
      }}
    >
      <svg
        width="100%"
        height="100%"
        style={{position: "absolute", inset: 0, pointerEvents: "none", overflow: "visible", touchAction: "none"}}
      >
        {/* Угловые скобки viewfinder */}
        <path
          d={`M ${x},${y + bracketLen} L ${x},${y} L ${x + bracketLen},${y}`}
          fill="none"
          stroke="rgba(255,255,255,0.9)"
          strokeWidth={3}
          strokeLinecap="round"
          strokeLinejoin="round"
        />
        <path
          d={`M ${x + w - bracketLen},${y} L ${x + w},${y} L ${x + w},${y + bracketLen}`}
          fill="none"
          stroke="rgba(255,255,255,0.9)"
          strokeWidth={3}
          strokeLinecap="round"
          strokeLinejoin="round"
        />
        <path
          d={`M ${x},${y + h - bracketLen} L ${x},${y + h} L ${x + bracketLen},${y + h}`}
          fill="none"
          stroke="rgba(255,255,255,0.9)"
          strokeWidth={3}
          strokeLinecap="round"
          strokeLinejoin="round"
        />
        <path
          d={`M ${x + w - bracketLen},${y + h} L ${x + w},${y + h} L ${x + w},${y + h - bracketLen}`}
          fill="none"
          stroke="rgba(255,255,255,0.9)"
          strokeWidth={3}
          strokeLinecap="round"
          strokeLinejoin="round"
        />

        {/* Полигоны найденных баркодов: циан внутри viewfinder, красный снаружи */}
        {barcodePolygons.map(({points, inViewfinder}) => (
          <polygon
            key={points}
            points={points}
            fill="none"
            stroke={inViewfinder ? "#00e5ff" : "#ff3d00"}
            strokeWidth={2.5}
            strokeLinejoin="round"
            style={{opacity: 1, transition: "opacity 0.15s"}}
          />
        ))}

        {/* Невидимые круговые хендлы для drag-resize по углам */}
        {onResize &&
          corners.map(({id, cx, cy, cursor, dxSign, dySign}) => (
            <circle
              key={id}
              cx={cx}
              cy={cy}
              r={HANDLE_RADIUS}
              fill="transparent"
              style={{cursor, pointerEvents: "all", touchAction: "none"}}
              onPointerDown={(e) => handlePointerDown(e, dxSign, dySign)}
              onPointerMove={handlePointerMove}
              onPointerUp={handlePointerUp}
              onPointerCancel={handlePointerUp}
            />
          ))}
      </svg>
    </div>
  );
};
