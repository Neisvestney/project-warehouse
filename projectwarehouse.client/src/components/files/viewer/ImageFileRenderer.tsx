import {useCallback, useLayoutEffect, useRef, useState} from "react";
import Box from "@mui/material/Box";
import CircularProgress from "@mui/material/CircularProgress";
import IconButton from "@mui/material/IconButton";
import Tooltip from "@mui/material/Tooltip";
import Typography from "@mui/material/Typography";
import ZoomInIcon from "@mui/icons-material/ZoomIn";
import ZoomOutIcon from "@mui/icons-material/ZoomOut";
import RestartAltIcon from "@mui/icons-material/RestartAlt";
import RotateLeftIcon from "@mui/icons-material/RotateLeft";
import RotateRightIcon from "@mui/icons-material/RotateRight";
import {
  TransformComponent,
  TransformWrapper,
  useControls,
  useTransformComponent,
} from "react-zoom-pan-pinch";
import type {ReactZoomPanPinchRef} from "react-zoom-pan-pinch";
import UnsupportedFileRenderer from "./UnsupportedFileRenderer";
import type {ResolvedViewable} from "./useViewableSource";

const MIN_SCALE = 1;
const MAX_SCALE = 8;

/** Zoom per unit of pinch delta. 0.01 is the rate browsers use for their own trackpad page zoom. */
const PINCH_ZOOM_RATE = 0.01;

interface Size {
  width: number;
  height: number;
}

type Layout = ReturnType<typeof fitLayout>;

/**
 * A trackpad two-finger scroll and a mouse wheel arrive as the same `ctrlKey`-less wheel event (a
 * trackpad *pinch* is the one the browser marks with `ctrlKey`), so only the shape of the delta
 * separates them: a wheel notch is coarse and quantized — ±100 in Chrome, `deltaMode: 1` in Firefox
 * — and never horizontal, while a trackpad emits fine, often fractional deltas with a `deltaX`.
 * High-resolution scroll wheels also emit fine deltas and are read as a trackpad here; that is the
 * price of the heuristic, and there is no API that gives the device away.
 */
const isTrackpadScroll = (e: WheelEvent) =>
  e.deltaMode === 0 && (e.deltaX !== 0 || !Number.isInteger(e.deltaY) || Math.abs(e.deltaY) < 50);

const clamp = (value: number, min: number, max: number) => Math.min(max, Math.max(min, value));

/**
 * Mirrors the library's own `getBounds` so a manual pan stops exactly where a dragged one does.
 * `content` is the scaled size; the halved slack is what `centerZoomedOut` does to the axis the
 * content is too small for, pinning it to the middle.
 */
const clampAxis = (position: number, wrapper: number, content: number) => {
  const diff = wrapper - content;
  const slack = wrapper > content ? diff / 2 : 0;
  return Math.min(slack, Math.max(diff - slack, position));
};

/**
 * Pan and zoom come from react-zoom-pan-pinch: wheel and trackpad pinch on desktop, two-finger
 * pinch and drag on touch, double click/tap to toggle zoom. A load error switches to the download
 * card — that is also the main degradation path for an external link that turned out not to be an
 * image.
 */
export default function ImageFileRenderer({item}: {item: ResolvedViewable}) {
  // rotation and the error flag are per-file state; the modal remounts this on navigation via key
  const [failed, setFailed] = useState(false);
  const [rotation, setRotation] = useState(0);
  const [box, setBox] = useState<Size | null>(null);
  // our own files carry the dimensions in the DTO, so the layout is known before the first paint
  // and the image never appears at an uncentered position
  const [natural, setNatural] = useState<Size | null>(() =>
    item.imageWidth && item.imageHeight ? {width: item.imageWidth, height: item.imageHeight} : null,
  );
  const transformRef = useRef<ReactZoomPanPinchRef>(null);
  const resetScale = useRef(false);

  const quarterTurn = rotation % 180 !== 0;
  // the rotated image is laid out through a wrapper of the post-rotation size, so the library
  // computes pan bounds from what is actually on screen
  const layout = box && natural ? fitLayout(natural, box, quarterTurn) : null;
  const footprint = layout && `${layout.boxWidth}x${layout.boxHeight}`;

  // a callback ref, not an effect: the container mounts only once the source resolves, which for
  // our own files happens after the spinner render
  const attachContainer = useCallback((node: HTMLDivElement | null) => {
    if (!node) return;

    // read once up front: the observer's first delivery is not guaranteed to land before the paint
    setBox({width: node.clientWidth, height: node.clientHeight});

    const observer = new ResizeObserver(([entry]) => {
      const {width, height} = entry.contentRect;
      setBox({width, height});
    });
    observer.observe(node);

    // capture phase, so it runs before the library's own listener on the wrapper below and can
    // take the event away from its wheel zoom
    const onWheel = (e: WheelEvent) => {
      // a mouse wheel, with or without ctrl, is left to the library: its per-notch step suits it
      if (!isTrackpadScroll(e)) return;

      e.stopPropagation();
      // without this the browser zooms the page on a pinch and swipes back on a horizontal scroll
      if (e.cancelable) e.preventDefault();

      // a two-finger scroll is not a zoom gesture; only a pinch (which the browser marks) is
      if (!e.ctrlKey) return;

      const ref = transformRef.current;
      const wrapper = ref?.instance.wrapperComponent;
      const content = ref?.instance.contentComponent;
      if (!ref || !wrapper || !content) return;

      // continuous and proportional to the gesture: the library's own wheel zoom flattens every
      // delta to a full step, which a pinch — firing at frame rate — turns into a jump
      const {positionX, positionY, scale} = ref.instance.state;
      const next = clamp(scale * Math.exp(-e.deltaY * PINCH_ZOOM_RATE), MIN_SCALE, MAX_SCALE);
      const ratio = next / scale;

      // anchor the point under the cursor so the image zooms where the fingers are
      const rect = wrapper.getBoundingClientRect();
      const anchorX = e.clientX - rect.left;
      const anchorY = e.clientY - rect.top;

      ref.setTransform(
        clampAxis(anchorX - (anchorX - positionX) * ratio, rect.width, content.offsetWidth * next),
        clampAxis(
          anchorY - (anchorY - positionY) * ratio,
          rect.height,
          content.offsetHeight * next,
        ),
        next,
        0,
      );
    };
    node.addEventListener("wheel", onWheel, {capture: true, passive: false});

    return () => {
      observer.disconnect();
      node.removeEventListener("wheel", onWheel, true);
    };
  }, []);

  // a layout effect, so the recentring lands in the same frame the new footprint does and the
  // image is never painted in the corner `centerOnInit` left it in. Rotation additionally drops
  // back to 1×; a resize keeps the zoom the user is on.
  useLayoutEffect(() => {
    if (!footprint) return;

    transformRef.current?.centerView(resetScale.current ? MIN_SCALE : undefined, 0);
    resetScale.current = false;
  }, [footprint, rotation]);

  const rotate = (dir: -1 | 1) => {
    resetScale.current = true;
    setRotation((r) => (r + dir * 90 + 360) % 360);
  };

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
      ref={attachContainer}
      sx={{position: "relative", height: "100%", overflow: "hidden", touchAction: "none"}}
    >
      <TransformWrapper
        ref={transformRef}
        minScale={MIN_SCALE}
        maxScale={MAX_SCALE}
        centerOnInit
        centerZoomedOut
        doubleClick={{mode: "toggle", step: 1}}
        wheel={{step: 0.2}}
      >
        <Surface
          item={item}
          layout={layout}
          rotation={rotation}
          onError={() => setFailed(true)}
          onNaturalSize={setNatural}
        />

        <Controls onRotate={rotate} />
      </TransformWrapper>
    </Box>
  );
}

function Surface({
  item,
  layout,
  rotation,
  onError,
  onNaturalSize,
}: {
  item: ResolvedViewable;
  layout: Layout | null;
  rotation: number;
  onError: () => void;
  onNaturalSize: (size: Size) => void;
}) {
  const zoomed = useTransformComponent(({state}) => state.scale > MIN_SCALE);

  return (
    <TransformComponent
      wrapperStyle={{width: "100%", height: "100%", cursor: zoomed ? "grab" : "default"}}
      contentStyle={
        layout
          ? {width: layout.boxWidth, height: layout.boxHeight, position: "relative"}
          : // an external source has no dimensions until it loads; centring keeps that first frame
            // in the same place the measured layout will put it
            {width: "100%", height: "100%", alignItems: "center", justifyContent: "center"}
      }
    >
      <Box
        component="img"
        src={item.src}
        alt={item.name}
        referrerPolicy={item.isExternal ? "no-referrer" : undefined}
        onError={onError}
        onLoad={(e) => {
          const img = e.currentTarget;
          onNaturalSize({width: img.naturalWidth, height: img.naturalHeight});
        }}
        draggable={false}
        style={
          layout
            ? {
                position: "absolute",
                left: "50%",
                top: "50%",
                width: layout.width,
                height: layout.height,
                transform: `translate(-50%, -50%) rotate(${rotation}deg)`,
              }
            : {maxWidth: "100%", maxHeight: "100%"}
        }
        sx={{objectFit: "contain", userSelect: "none"}}
      />
    </TransformComponent>
  );
}

/** Contain-fit into the box, measured against the swapped axes for a quarter turn, never upscaled. */
function fitLayout(natural: Size, box: Size, quarterTurn: boolean) {
  const availableWidth = quarterTurn ? box.height : box.width;
  const availableHeight = quarterTurn ? box.width : box.height;
  const fit = Math.min(availableWidth / natural.width, availableHeight / natural.height, 1);
  const width = natural.width * fit;
  const height = natural.height * fit;

  return {
    width,
    height,
    boxWidth: quarterTurn ? height : width,
    boxHeight: quarterTurn ? width : height,
  };
}

function Controls({onRotate}: {onRotate: (dir: -1 | 1) => void}) {
  const {zoomIn, zoomOut, resetTransform} = useControls();
  const scale = useTransformComponent(({state}) => state.scale);

  return (
    <Box
      sx={{
        position: "absolute",
        bottom: 12,
        left: "50%",
        transform: "translateX(-50%)",
        display: "flex",
        alignItems: "center",
        gap: 0.5,
        px: 0.5,
        borderRadius: 999,
        bgcolor: "rgba(0,0,0,0.55)",
        backdropFilter: "blur(4px)",
        color: "common.white",
      }}
    >
      <Tooltip title="Повернуть влево">
        <IconButton size="small" sx={{color: "inherit"}} onClick={() => onRotate(-1)}>
          <RotateLeftIcon fontSize="small" />
        </IconButton>
      </Tooltip>
      <Tooltip title="Повернуть вправо">
        <IconButton size="small" sx={{color: "inherit"}} onClick={() => onRotate(1)}>
          <RotateRightIcon fontSize="small" />
        </IconButton>
      </Tooltip>

      <Box sx={{width: "1px", alignSelf: "stretch", my: 1, bgcolor: "rgba(255,255,255,0.24)"}} />

      <Tooltip title="Уменьшить">
        <span>
          <IconButton
            size="small"
            sx={{color: "inherit"}}
            disabled={scale <= MIN_SCALE}
            onClick={() => zoomOut()}
          >
            <ZoomOutIcon fontSize="small" />
          </IconButton>
        </span>
      </Tooltip>

      <Typography
        variant="caption"
        sx={{
          display: {xs: "none", sm: "block"},
          minWidth: 44,
          textAlign: "center",
          fontVariantNumeric: "tabular-nums",
        }}
      >
        {Math.round(scale * 100)}%
      </Typography>

      <Tooltip title="Увеличить">
        <span>
          <IconButton
            size="small"
            sx={{color: "inherit"}}
            disabled={scale >= MAX_SCALE}
            onClick={() => zoomIn()}
          >
            <ZoomInIcon fontSize="small" />
          </IconButton>
        </span>
      </Tooltip>

      <Tooltip title="Сбросить">
        <span>
          <IconButton
            size="small"
            sx={{color: "inherit"}}
            disabled={scale <= MIN_SCALE}
            onClick={() => resetTransform()}
          >
            <RestartAltIcon fontSize="small" />
          </IconButton>
        </span>
      </Tooltip>
    </Box>
  );
}
