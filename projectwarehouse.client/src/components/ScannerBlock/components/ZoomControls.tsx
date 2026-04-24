import React from "react";

type ScanMode = "loading" | "camera" | "upload";

interface ZoomControlsProps {
  mode: ScanMode;
  zoomLevel: number;
  zoomCapabilities: {min: number; max: number; supported: boolean};
  onZoomIn: () => void;
  onZoomOut: () => void;
}

/**
 * Кнопки управления зумом камеры
 *
 * - Отображаются только в режиме 'camera' и если камера поддерживает зум
 * - Кнопка + увеличивает зум
 * - Кнопка − уменьшает зум
 * - Disabled state при достижении min/max зума
 */
export const ZoomControls: React.FC<ZoomControlsProps> = ({
  mode,
  zoomLevel,
  zoomCapabilities,
  onZoomIn,
  onZoomOut,
}) => {
  if (mode !== "camera" || !zoomCapabilities.supported) return null;

  return (
    <></>
    // <div>
    //   <button
    //     onClick={onZoomIn}
    //     aria-label="Увеличить"
    //     disabled={zoomLevel >= zoomCapabilities.max}
    //   >
    //     {/*<Icon name="plus" />*/} +
    //   </button>
    //   <button
    //     onClick={onZoomOut}
    //     aria-label="Уменьшить"
    //     disabled={zoomLevel <= zoomCapabilities.min}
    //   >
    //     {/*<Icon name="minus" />*/} -
    //   </button>
    // </div>
  );
};
