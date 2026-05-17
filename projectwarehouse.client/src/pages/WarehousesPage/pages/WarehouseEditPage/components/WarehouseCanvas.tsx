import React, {useEffect, useRef, useState} from "react";
import {observer} from "mobx-react-lite";
import {Box, IconButton, Paper, Tooltip, Typography} from "@mui/material";
import {Rect, Text, Transformer} from "react-konva";
import type Konva from "konva";
import {blue, green, grey, orange} from "@mui/material/colors";
import MyLocationIcon from "@mui/icons-material/MyLocation";
import DeleteIcon from "@mui/icons-material/Delete";
import StageWithPanAndZoom, {
  type StageWithPanAndZoomHandle,
} from "@/components/StageWithPanAndZoom";
import type {WarehouseLayoutObjectType} from "@/api/types.gen";
import {useWarehouseEditStore} from "../WarehouseEditStoreContext";
import type {Tool} from "../warehouseEditStore";
import ObjectPropertiesDialog from "./ObjectPropertiesDialog";

const LAYOUT_OBJECT_STYLE: Record<WarehouseLayoutObjectType, {fill: string; stroke: string}> = {
  wall: {fill: grey[700], stroke: grey[800]},
  passage: {fill: orange[100], stroke: orange[300]},
};

const DRAW_PREVIEW_FILL: Record<Exclude<Tool, "select">, string> = {
  storagePlace: green[200],
  wall: grey[500],
  passage: orange[200],
};

interface DrawPos {
  x: number;
  y: number;
}

const CANVAS_SCALE = 100;
const MIN_DRAW_PX = 5;

export default observer(function WarehouseCanvas() {
  const store = useWarehouseEditStore();

  const containerRef = useRef<HTMLDivElement>(null);
  const stageRef = useRef<StageWithPanAndZoomHandle>(null);
  const [stageScale, setStageScale] = useState({x: 1, y: 1});

  const shapeRefs = useRef<Map<string, Konva.Rect>>(new Map());
  const transformerRef = useRef<Konva.Transformer>(null);

  const drawStartRef = useRef<DrawPos | null>(null);
  const [drawPreview, setDrawPreview] = useState<{
    x: number;
    y: number;
    w: number;
    h: number;
  } | null>(null);

  const [propertiesDialogTempId, setPropertiesDialogTempId] = useState<string | null>(null);
  const [draggingTempId, setDraggingTempId] = useState<string | null>(null);

  const warehouseWidth = (store.form.data?.width ?? 10) * CANVAS_SCALE;
  const warehouseHeight = (store.form.data?.height ?? 10) * CANVAS_SCALE;

  // Fit view once after initial data loads
  const fittedRef = useRef(false);
  useEffect(() => {
    if (warehouseWidth > 0 && !fittedRef.current) {
      fittedRef.current = true;
      requestAnimationFrame(() => stageRef.current?.fit());
    }
  }, [warehouseWidth]);

  // Wire Konva Transformer to the selected shape
  useEffect(() => {
    if (!transformerRef.current) return;
    const node = store.selectedTempId ? shapeRefs.current.get(store.selectedTempId) : undefined;
    transformerRef.current.nodes(node ? [node] : []);
    transformerRef.current.getLayer()?.batchDraw();
  }, [store.selectedTempId]);

  // Clear draw state when tool changes
  useEffect(() => {
    drawStartRef.current = null;
    setDrawPreview(null);
  }, [store.activeTool]);

  // Delete key removes selected object
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      const target = e.target as HTMLElement;
      if (target.tagName === "INPUT" || target.tagName === "TEXTAREA") return;
      if (e.key === "Delete" || e.key === "Backspace") store.deleteSelected();
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [store]);

  const handleStageMouseDown = (e: Konva.KonvaEventObject<MouseEvent>) => {
    const targetName = (e.target as Konva.Shape).name?.() ?? "";
    const isOurShape = targetName === "canvas-shape";
    // Transformer handles have names like "top-left", "rotater", "back" etc. (non-empty, not ours)
    const isOtherNamedNode = targetName !== "" && !isOurShape;

    if (store.activeTool === "select") {
      if (isOurShape || isOtherNamedNode) return; // shape's onClick / Transformer handles — don't touch
      store.selectObject(null);
      return;
    }

    if (isOurShape || isOtherNamedNode) return;

    const pos = e.target.getStage()?.getRelativePointerPosition();
    if (!pos) return;
    drawStartRef.current = pos;
    setDrawPreview({x: pos.x, y: pos.y, w: 0, h: 0});
  };

  const handleStageMouseMove = (e: Konva.KonvaEventObject<MouseEvent>) => {
    if (!drawStartRef.current || store.activeTool === "select") return;
    const pos = e.target.getStage()?.getRelativePointerPosition();
    if (!pos) return;
    const start = drawStartRef.current;
    setDrawPreview({
      x: Math.min(start.x, pos.x),
      y: Math.min(start.y, pos.y),
      w: Math.abs(pos.x - start.x),
      h: Math.abs(pos.y - start.y),
    });
  };

  const handleStageMouseUp = (_e: Konva.KonvaEventObject<MouseEvent>) => {
    if (!drawStartRef.current || store.activeTool === "select") return;
    drawStartRef.current = null;

    // Capture preview before scheduling its clear — state updates are async
    const preview = drawPreview;
    setDrawPreview(null);

    if (!preview || preview.w < MIN_DRAW_PX || preview.h < MIN_DRAW_PX) return;

    const {x, y, w, h} = preview;
    if (store.activeTool === "storagePlace") {
      store.addStoragePlace({
        name: `Место ${store.storagePlaces.length + 1}`,
        x: x / CANVAS_SCALE,
        y: y / CANVAS_SCALE,
        width: w / CANVAS_SCALE,
        height: h / CANVAS_SCALE,
        rotation: 0,
      });
    } else {
      store.addLayoutObject({
        type: store.activeTool as WarehouseLayoutObjectType,
        x: x / CANVAS_SCALE,
        y: y / CANVAS_SCALE,
        width: w / CANVAS_SCALE,
        height: h / CANVAS_SCALE,
        rotation: 0,
      });
    }
  };

  const makeTransformEndHandler = (tempId: string, kind: "storagePlace" | "layoutObject") => () => {
    const node = shapeRefs.current.get(tempId);
    if (!node) return;

    const newWidthPx = Math.max(10, node.width() * node.scaleX());
    const newHeightPx = Math.max(10, node.height() * node.scaleY());

    // Capture world position of local (0,0) before touching anything.
    // This corner must stay fixed through our offset/size reset.
    const angle = node.rotation() * (Math.PI / 180);
    const cos = Math.cos(angle);
    const sin = Math.sin(angle);
    const scaledOx = node.offsetX() * node.scaleX();
    const scaledOy = node.offsetY() * node.scaleY();
    const topLeftX = node.x() - scaledOx * cos + scaledOy * sin;
    const topLeftY = node.y() - scaledOx * sin - scaledOy * cos;

    node.scaleX(1);
    node.scaleY(1);
    node.width(newWidthPx);
    node.height(newHeightPx);
    node.offsetX(newWidthPx / 2);
    node.offsetY(newHeightPx / 2);

    // Re-anchor so local (0,0) stays at topLeft (avoids visual jump on rotation).
    node.x(topLeftX + (newWidthPx / 2) * cos - (newHeightPx / 2) * sin);
    node.y(topLeftY + (newWidthPx / 2) * sin + (newHeightPx / 2) * cos);

    const updates = {
      x: (node.x() - newWidthPx / 2) / CANVAS_SCALE,
      y: (node.y() - newHeightPx / 2) / CANVAS_SCALE,
      width: newWidthPx / CANVAS_SCALE,
      height: newHeightPx / CANVAS_SCALE,
      rotation: node.rotation(),
    };

    if (kind === "storagePlace") store.updateStoragePlace(tempId, updates);
    else store.updateLayoutObject(tempId, updates);
  };

  const makeDragEndHandler =
    (tempId: string, kind: "storagePlace" | "layoutObject") =>
    (e: Konva.KonvaEventObject<DragEvent>) => {
      e.cancelBubble = true;
      setDraggingTempId(null);
      const node = e.target as Konva.Rect;
      const wPx = node.width();
      const hPx = node.height();
      const updates = {
        x: (node.x() - wPx / 2) / CANVAS_SCALE,
        y: (node.y() - hPx / 2) / CANVAS_SCALE,
      };
      if (kind === "storagePlace") store.updateStoragePlace(tempId, updates);
      else store.updateLayoutObject(tempId, updates);
    };

  const makeClickHandler = (tempId: string) => (e: Konva.KonvaEventObject<Event>) => {
    if (store.activeTool !== "select") return;
    e.cancelBubble = true;
    store.selectObject(tempId);
  };

  const makeContextMenuHandler = (tempId: string) => (e: Konva.KonvaEventObject<MouseEvent>) => {
    e.evt.preventDefault();
    e.cancelBubble = true;
    store.selectObject(tempId);
    setPropertiesDialogTempId(tempId);
  };

  const setShapeRef = (tempId: string) => (node: Konva.Rect | null) => {
    if (node) shapeRefs.current.set(tempId, node);
    else shapeRefs.current.delete(tempId);
  };

  const isSelectTool = store.activeTool === "select";
  const selectedObj = store.selectedObject;

  return (
    <Box>
      <Paper
        ref={containerRef}
        sx={{
          width: "100%",
          height: "calc(100vh - 390px)",
          minHeight: 420,
          position: "relative",
          cursor: isSelectTool ? "default" : "crosshair",
        }}
      >
        <StageWithPanAndZoom
          containerRef={containerRef}
          ref={stageRef}
          setStageScale={setStageScale}
          draggable={isSelectTool && !selectedObj}
          panOnEmptyOnly={isSelectTool && !!selectedObj}
          onMouseDown={handleStageMouseDown}
          onMouseMove={handleStageMouseMove}
          onMouseUp={handleStageMouseUp}
        >
          {/* Warehouse boundary */}
          <Rect
            x={0}
            y={0}
            width={warehouseWidth}
            height={warehouseHeight}
            stroke={blue[300]}
            dash={[10 / stageScale.x]}
            listening={false}
          />

          {/* Layout objects (walls, passages) */}
          {store.layoutObjects.map((lo) => {
            const wPx = lo.width * CANVAS_SCALE;
            const hPx = lo.height * CANVAS_SCALE;
            const isSelected = lo.tempId === store.selectedTempId;
            return (
              <Rect
                key={lo.tempId}
                ref={setShapeRef(lo.tempId)}
                name="canvas-shape"
                x={lo.x * CANVAS_SCALE + wPx / 2}
                y={lo.y * CANVAS_SCALE + hPx / 2}
                offsetX={wPx / 2}
                offsetY={hPx / 2}
                width={wPx}
                height={hPx}
                rotation={lo.rotation}
                {...LAYOUT_OBJECT_STYLE[lo.type]}
                strokeWidth={isSelected ? 2 : 1}
                draggable={
                  isSelectTool &&
                  store.selectedObject?.kind == "layoutObject" &&
                  store.selectedObject.data.tempId == lo.tempId
                }
                onClick={makeClickHandler(lo.tempId)}
                onTap={makeClickHandler(lo.tempId)}
                onContextMenu={makeContextMenuHandler(lo.tempId)}
                onDragEnd={makeDragEndHandler(lo.tempId, "layoutObject")}
                onTransformEnd={makeTransformEndHandler(lo.tempId, "layoutObject")}
              />
            );
          })}

          {/* Storage places */}
          {store.storagePlaces.map((sp) => {
            const wPx = sp.width * CANVAS_SCALE;
            const hPx = sp.height * CANVAS_SCALE;
            const isSelected = sp.tempId === store.selectedTempId;
            return (
              <React.Fragment key={sp.tempId}>
                <Rect
                  ref={setShapeRef(sp.tempId)}
                  name="canvas-shape"
                  x={sp.x * CANVAS_SCALE + wPx / 2}
                  y={sp.y * CANVAS_SCALE + hPx / 2}
                  offsetX={wPx / 2}
                  offsetY={hPx / 2}
                  width={wPx}
                  height={hPx}
                  rotation={sp.rotation}
                  fill={green[300]}
                  stroke={green[700]}
                  strokeWidth={isSelected ? 2 : 1}
                  draggable={
                    isSelectTool &&
                    store.selectedObject?.kind == "storagePlace" &&
                    store.selectedObject.data.tempId == sp.tempId
                  }
                  onClick={makeClickHandler(sp.tempId)}
                  onTap={makeClickHandler(sp.tempId)}
                  onContextMenu={makeContextMenuHandler(sp.tempId)}
                  onDragStart={() => setDraggingTempId(sp.tempId)}
                  onDragEnd={makeDragEndHandler(sp.tempId, "storagePlace")}
                  onTransformEnd={makeTransformEndHandler(sp.tempId, "storagePlace")}
                />
                <Text
                  x={sp.x * CANVAS_SCALE + wPx / 2}
                  y={sp.y * CANVAS_SCALE + hPx / 2}
                  offsetX={wPx / 2}
                  offsetY={hPx / 2}
                  width={wPx}
                  height={hPx}
                  rotation={sp.rotation}
                  align="center"
                  verticalAlign="middle"
                  text={sp.name}
                  listening={false}
                  visible={draggingTempId !== sp.tempId}
                />
              </React.Fragment>
            );
          })}

          {/* Draw preview */}
          {drawPreview && store.activeTool !== "select" && (
            <Rect
              x={drawPreview.x}
              y={drawPreview.y}
              width={drawPreview.w}
              height={drawPreview.h}
              fill={DRAW_PREVIEW_FILL[store.activeTool as Exclude<Tool, "select">]}
              opacity={0.6}
              listening={false}
            />
          )}

          {/* Selection transformer */}
          <Transformer
            ref={transformerRef}
            rotateEnabled={true}
            boundBoxFunc={(oldBox, newBox) => {
              if (newBox.width < 10 || newBox.height < 10) return oldBox;
              return newBox;
            }}
          />
        </StageWithPanAndZoom>

        {/* Canvas controls overlay */}
        <Box
          sx={{
            position: "absolute",
            top: 8,
            right: 8,
            display: "flex",
            flexDirection: "column",
            gap: 0.5,
          }}
        >
          <Tooltip title="Отцентровать">
            <IconButton size="small" onClick={() => stageRef.current?.fit()}>
              <MyLocationIcon fontSize="small" />
            </IconButton>
          </Tooltip>
          {store.selectedTempId && (
            <Tooltip title="Удалить выбранный объект (Delete)">
              <IconButton size="small" color="error" onClick={() => store.deleteSelected()}>
                <DeleteIcon fontSize="small" />
              </IconButton>
            </Tooltip>
          )}
        </Box>

        {/* Selected object info */}
        {selectedObj && (
          <Box
            sx={{
              position: "absolute",
              bottom: 8,
              left: 8,
              bgcolor: "background.paper",
              border: 1,
              borderColor: "divider",
              borderRadius: 1,
              px: 1.5,
              py: 0.5,
            }}
          >
            <Typography variant="caption" color="text.secondary">
              {selectedObj.kind === "storagePlace"
                ? `Место хранения: ${selectedObj.data.name}`
                : `Объект: ${selectedObj.data.type === "wall" ? "Стена" : "Проход"}`}
              {" · "}
              {selectedObj.data.width.toFixed(2)}×{selectedObj.data.height.toFixed(2)} м
              {selectedObj.data.rotation !== 0 && ` · ${selectedObj.data.rotation.toFixed(1)}°`}
              {" · ПКМ для редактирования"}
            </Typography>
          </Box>
        )}
      </Paper>

      <ObjectPropertiesDialog
        open={propertiesDialogTempId !== null}
        tempId={propertiesDialogTempId}
        onClose={() => setPropertiesDialogTempId(null)}
      />
    </Box>
  );
});
