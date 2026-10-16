import React, {useEffect, useRef, useState} from "react";
import {Box, IconButton, Stack, Tooltip} from "@mui/material";
import {Rect, Text} from "react-konva";
import {blue, grey, orange} from "@mui/material/colors";
import {type WarehouseLayoutElementDto, type WarehouseLayoutObjectType} from "@/api/types.gen.ts";
import StageWithPanAndZoom, {
  type StageWithPanAndZoomHandle,
} from "@/components/StageWithPanAndZoom.tsx";
import MyLocationIcon from "@mui/icons-material/MyLocation";

const layoutObjectStyle: Record<WarehouseLayoutObjectType, {fill: string; stroke: string}> = {
  wall: {fill: grey[700], stroke: grey[800]},
  passage: {fill: orange[100], stroke: orange[300]},
};

export interface WarehouseStoragePlaceRenderItem {
  id: string;
  x: number;
  y: number;
  width: number;
  height: number;
  rotation: number;
  name: string;
  fill: string;
  label?: string;
}

export interface WarehouseCanvasProps {
  width: number;
  height: number;
  layoutObjects: WarehouseLayoutElementDto[];
  storagePlaces: WarehouseStoragePlaceRenderItem[];
  onStoragePlaceClick?: (id: string) => void;
}

function WarehouseCanvas({
  width,
  height,
  layoutObjects,
  storagePlaces,
  onStoragePlaceClick,
}: WarehouseCanvasProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const stageRef = useRef<StageWithPanAndZoomHandle>(null);
  const [stageScale, setStageScale] = useState({x: 1, y: 1});
  const fitted = useRef(false);

  useEffect(() => {
    if (!stageRef.current) return;
    if (fitted.current) return;
    fitted.current = true;
    requestAnimationFrame(() => stageRef.current?.fit());
  }, [width, height]);

  return (
    <Box ref={containerRef} sx={{position: "relative", width: "100%", height: "100%"}}>
      <StageWithPanAndZoom containerRef={containerRef} ref={stageRef} setStageScale={setStageScale}>
        <Rect
          x={0}
          y={0}
          width={width * 100}
          height={height * 100}
          stroke={blue[300]}
          dash={[10 / stageScale.x]}
        />
        {layoutObjects.map((lo, i) => (
          <Rect
            key={i}
            x={lo.x * 100 + (lo.width * 100) / 2}
            y={lo.y * 100 + (lo.height * 100) / 2}
            offsetX={(lo.width * 100) / 2}
            offsetY={(lo.height * 100) / 2}
            width={lo.width * 100}
            height={lo.height * 100}
            rotation={lo.rotation}
            {...layoutObjectStyle[lo.type]}
          />
        ))}
        {storagePlaces.map((p) => (
          <React.Fragment key={p.id}>
            <Rect
              x={p.x * 100 + (p.width * 100) / 2}
              y={p.y * 100 + (p.height * 100) / 2}
              width={p.width * 100}
              height={p.height * 100}
              offsetX={(p.width * 100) / 2}
              offsetY={(p.height * 100) / 2}
              fill={p.fill}
              onClick={() => onStoragePlaceClick?.(p.id)}
              onTap={() => onStoragePlaceClick?.(p.id)}
              rotation={p.rotation}
            />
            <Text
              x={p.x * 100 + (p.width * 100) / 2}
              y={p.y * 100 + (p.height * 100) / 2}
              width={p.width * 100}
              height={p.height * 100}
              offsetX={(p.width * 100) / 2}
              offsetY={(p.height * 100) / 2}
              align="center"
              verticalAlign="middle"
              text={p.label ?? p.name}
              onClick={() => onStoragePlaceClick?.(p.id)}
              onTap={() => onStoragePlaceClick?.(p.id)}
              rotation={p.rotation}
            />
          </React.Fragment>
        ))}
      </StageWithPanAndZoom>
      <Stack sx={{position: "absolute", top: 10, right: 10}}>
        <Tooltip title="Отцентровать">
          <IconButton onClick={() => stageRef.current?.fit()}>
            <MyLocationIcon />
          </IconButton>
        </Tooltip>
      </Stack>
    </Box>
  );
}

export default WarehouseCanvas;
