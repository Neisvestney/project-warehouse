import {Stage, Layer} from "react-konva";
import React, {
  useState,
  useEffect,
  useCallback,
  useRef,
  useImperativeHandle,
  forwardRef,
  type RefObject,
} from "react";
import Konva from "konva";

// by default Konva prevent some events when node is dragging
// it improve the performance and work well for 95% of cases
// we need to enable all events on Konva, even when we are dragging a node
// so it triggers touchmove correctly
Konva.hitOnDragEnabled = true;

type Vec = {x: number; y: number};

export interface StageWithPanAndZoomHandle {
  fit: () => void;
}

export interface StageWithPanAndZoomProps {
  containerRef: RefObject<HTMLDivElement | null>;
  children: React.ReactNode;
  setStageScale?: (scale: {x: number; y: number}) => void;
}

const StageWithPanAndZoom = forwardRef<StageWithPanAndZoomHandle, StageWithPanAndZoomProps>(
  function StageWithPanAndZoom({containerRef, children, setStageScale: setStageScaleParent}, ref) {
    const stageRef = useRef<Konva.Stage>(null);

    const [size, setSize] = useState({width: 0, height: 0});

    useEffect(() => {
      const el = containerRef.current;
      if (!el) return;
      const observer = new ResizeObserver(([entry]) => {
        const {width, height} = entry.contentRect;
        setSize({width, height});
      });
      observer.observe(el);
      setSize({width: el.clientWidth, height: el.clientHeight});
      return () => observer.disconnect();
    }, [containerRef]);

    const [stagePos, setStagePos] = useState({x: 0, y: 0});
    const [stageScale, setStageScale] = useState({x: 1, y: 1});

    useEffect(() => {
      setStageScaleParent?.(stageScale);
    }, [setStageScaleParent, stageScale]);

    useImperativeHandle(
      ref,
      () => ({
        fit() {
          const stage = stageRef.current;
          if (!stage) return;
          const layer = stage.getLayers()[0];
          if (!layer) return;
          const box = layer.getClientRect({relativeTo: stage});
          if (box.width === 0 || box.height === 0) return;
          const padding = 40;
          const scaleX = (size.width - padding * 2) / box.width;
          const scaleY = (size.height - padding * 2) / box.height;
          const newScale = Math.min(scaleX, scaleY);
          setStageScale({x: newScale, y: newScale});
          setStagePos({
            x: (size.width - box.width * newScale) / 2 - box.x * newScale,
            y: (size.height - box.height * newScale) / 2 - box.y * newScale,
          });
        },
      }),
      [size],
    );
    const [lastCenter, setLastCenter] = useState<Vec | null>(null);
    const [lastDist, setLastDist] = useState(0);
    const [dragStopped, setDragStopped] = useState(false);

    const getDistance = (p1: Vec, p2: Vec) => {
      return Math.sqrt(Math.pow(p2.x - p1.x, 2) + Math.pow(p2.y - p1.y, 2));
    };

    const getCenter = (p1: Vec, p2: Vec) => {
      return {
        x: (p1.x + p2.x) / 2,
        y: (p1.y + p2.y) / 2,
      };
    };

    const handleTouchMove = useCallback(
      (e: Konva.KonvaEventObject<TouchEvent>) => {
        e.evt.preventDefault();
        const touch1 = e.evt.touches[0];
        const touch2 = e.evt.touches[1];
        const stage = e.target.getStage();
        if (!stage) return;

        // we need to restore dragging, if it was cancelled by multi-touch
        if (touch1 && !touch2 && !stage.isDragging() && dragStopped) {
          stage.startDrag();
          setDragStopped(false);
        }

        if (touch1 && touch2) {
          // if the stage was under Konva's drag&drop
          // we need to stop it, and implement our own pan logic with two pointers
          if (stage.isDragging()) {
            stage.stopDrag();
            setDragStopped(true);
          }

          const rect = stage.container().getBoundingClientRect();

          const p1 = {
            x: touch1.clientX - rect.left,
            y: touch1.clientY - rect.top,
          };
          const p2 = {
            x: touch2.clientX - rect.left,
            y: touch2.clientY - rect.top,
          };

          if (!lastCenter) {
            setLastCenter(getCenter(p1, p2));
            return;
          }
          const newCenter = getCenter(p1, p2);

          const dist = getDistance(p1, p2);

          if (!lastDist) {
            setLastDist(dist);
            return;
          }

          // local coordinates of center point
          const pointTo = {
            x: (newCenter.x - stagePos.x) / stageScale.x,
            y: (newCenter.y - stagePos.y) / stageScale.x,
          };

          const scale = stageScale.x * (dist / lastDist);

          setStageScale({x: scale, y: scale});

          // calculate new position of the stage
          const dx = newCenter.x - lastCenter.x;
          const dy = newCenter.y - lastCenter.y;

          setStagePos({
            x: newCenter.x - pointTo.x * scale + dx,
            y: newCenter.y - pointTo.y * scale + dy,
          });

          setLastDist(dist);
          setLastCenter(newCenter);
        }
      },
      [dragStopped, lastCenter, lastDist, stagePos, stageScale],
    );

    const handleTouchEnd = () => {
      setLastDist(0);
      setLastCenter(null);
    };

    const handleWheel = useCallback((e: Konva.KonvaEventObject<WheelEvent>) => {
      e.evt.preventDefault();
      const stage = e.target.getStage();
      if (!stage) return;

      const scaleBy = 1.05;
      const oldScale = stage.scaleX();
      const pointer = stage.getPointerPosition();
      if (!pointer) return;

      const mousePointTo = {
        x: (pointer.x - stage.x()) / oldScale,
        y: (pointer.y - stage.y()) / oldScale,
      };

      const newScale = e.evt.deltaY < 0 ? oldScale * scaleBy : oldScale / scaleBy;

      setStageScale({x: newScale, y: newScale});
      setStagePos({
        x: pointer.x - mousePointTo.x * newScale,
        y: pointer.y - mousePointTo.y * newScale,
      });
    }, []);

    const handleDragEnd = (e: Konva.KonvaEventObject<DragEvent>) => {
      const stage = e.target.getStage();
      if (!stage) return;
      setDragStopped(false);
      // Ensure stage position is synchronized with our reactive state
      setStagePos({x: stage.x(), y: stage.y()});
    };

    return (
      <Stage
        ref={stageRef}
        width={size.width}
        height={size.height}
        draggable
        x={stagePos.x}
        y={stagePos.y}
        scaleX={stageScale.x}
        scaleY={stageScale.y}
        onWheel={handleWheel}
        onTouchMove={handleTouchMove}
        onTouchEnd={handleTouchEnd}
        onDragEnd={handleDragEnd}
      >
        <Layer>{children}</Layer>
      </Stage>
    );
  },
);

export default StageWithPanAndZoom;
