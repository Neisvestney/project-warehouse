import {useRef, useState} from "react";
import Box from "@mui/material/Box";
import CircularProgress from "@mui/material/CircularProgress";
import {useDoubleTap} from "use-double-tap";
import UnsupportedFileRenderer from "./UnsupportedFileRenderer";
import type {ResolvedViewable} from "./useViewableSource";

const MIN_SCALE = 1;
const MAX_SCALE = 8;

const clamp = (value: number, min: number, max: number) => Math.min(max, Math.max(min, value));

/**
 * Zoom and pan are hand-rolled: there is no lightbox library in the project and this is a couple of
 * handlers. A load error switches to the download card — that is also the main degradation path for
 * an external link that turned out not to be an image.
 */
export default function ImageFileRenderer({item}: {item: ResolvedViewable}) {
  // zoom, pan and the error flag are per-file state; the modal remounts this on navigation via key
  const [scale, setScale] = useState(1);
  const [offset, setOffset] = useState({x: 0, y: 0});
  const [failed, setFailed] = useState(false);
  const [dragging, setDragging] = useState(false);
  const dragStart = useRef<{x: number; y: number} | null>(null);

  const reset = () => {
    setScale(1);
    setOffset({x: 0, y: 0});
  };

  const endDrag = () => {
    dragStart.current = null;
    setDragging(false);
  };

  const doubleTap = useDoubleTap(reset);

  if (failed) return <UnsupportedFileRenderer item={item} />;

  if (!item.src) {
    return (
      <Box sx={{display: "flex", alignItems: "center", justifyContent: "center", height: "100%"}}>
        <CircularProgress sx={{color: "common.white"}} />
      </Box>
    );
  }

  return (
    <Box
      onWheel={(e) => {
        const next = clamp(scale * (e.deltaY < 0 ? 1.15 : 1 / 1.15), MIN_SCALE, MAX_SCALE);
        setScale(next);
        if (next === MIN_SCALE) setOffset({x: 0, y: 0});
      }}
      onPointerDown={(e) => {
        if (scale === MIN_SCALE) return;
        dragStart.current = {x: e.clientX - offset.x, y: e.clientY - offset.y};
        setDragging(true);
        e.currentTarget.setPointerCapture(e.pointerId);
      }}
      onPointerMove={(e) => {
        if (!dragStart.current) return;
        setOffset({x: e.clientX - dragStart.current.x, y: e.clientY - dragStart.current.y});
      }}
      onPointerUp={endDrag}
      onPointerCancel={endDrag}
      {...doubleTap}
      sx={{
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        height: "100%",
        overflow: "hidden",
        touchAction: "none",
        cursor: scale > MIN_SCALE ? "grab" : "default",
      }}
    >
      <Box
        component="img"
        src={item.src}
        alt={item.name}
        referrerPolicy={item.isExternal ? "no-referrer" : undefined}
        onError={() => setFailed(true)}
        draggable={false}
        style={{
          transform: `translate(${offset.x}px, ${offset.y}px) scale(${scale})`,
          transition: dragging ? "none" : "transform 120ms",
        }}
        sx={{maxWidth: "100%", maxHeight: "100%", objectFit: "contain", userSelect: "none"}}
      />
    </Box>
  );
}
