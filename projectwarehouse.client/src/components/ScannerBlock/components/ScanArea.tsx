import {Box, CircularProgress, css, Stack, styled, Typography} from "@mui/material";
import NoPhotographyIcon from "@mui/icons-material/NoPhotography";
import React from "react";

type ScanMode = "loading" | "camera" | "upload";

interface ScanAreaProps {
  mode: ScanMode;
  cameraError: string | null;
  isPortrait: boolean;
  isFocusing: boolean;
  videoRef: React.RefObject<HTMLVideoElement | null>;
  scanAreaRef: React.RefObject<HTMLDivElement | null>;
  doubleTapBind: Record<string, any>;
  onWheelZoom: (e: React.WheelEvent) => void;
  zoomControls: React.ReactNode;
}

/**
 * Область сканирования QR-кода
 *
 * Три режима:
 * - 'loading': Спиннер инициализации камеры
 * - 'camera': Активная камера с overlay (рамка, hint) и кнопками зума
 * - 'upload': Сообщение об ошибке камеры (~fallback на загрузку файла~)
 */
export const ScanArea: React.FC<ScanAreaProps> = ({
  mode,
  cameraError,
  isPortrait,
  isFocusing,
  videoRef,
  scanAreaRef,
  doubleTapBind,
  onWheelZoom,
  zoomControls,
}) => {
  if (mode === "upload") {
    return (
      <ScanAreaDiv>
        <CenterMessageBox>
          <Stack spacing={1} direction="row" sx={{alignItems: "center"}}>
            <NoPhotographyIcon sx={{fontSize: 36}} />
            <Typography variant={"h5"}>{cameraError || "Ошибка"}</Typography>
          </Stack>
        </CenterMessageBox>
      </ScanAreaDiv>
    );
  }

  return (
    <ScanAreaDiv
      ref={scanAreaRef}
      // className={cn(styles.scan_area, {
      //   [styles.loading_state]: mode === "loading",
      //   [styles.camera_active]: mode === "camera",
      //   [styles.portrait_crop]: isPortrait && mode === "camera",
      // })}
      onWheel={onWheelZoom}
    >
      <Video
        ref={videoRef}
        style={mode === "loading" ? {opacity: 0, position: "absolute"} : undefined}
        {...doubleTapBind}
        playsInline
        muted
      />

      {mode === "loading" && (
        <CenterMessageBox>
          <Stack spacing={1} direction="row" sx={{alignItems: "center"}}>
            <CircularProgress aria-label="Loading…" />
            <Typography variant={"h5"}>Инициализация камеры...</Typography>
          </Stack>
        </CenterMessageBox>
      )}

      {/*{mode === "camera" && (*/}
      {/*  <div>*/}
      {/*    <div*/}
      {/*    // className={cn(styles.scan_frame, {*/}
      {/*    //   [styles.focusing]: isFocusing,*/}
      {/*    // })}*/}
      {/*    />*/}
      {/*    <div>*/}
      {/*      <p>наведите камеру на QR код</p>*/}
      {/*    </div>*/}
      {/*  </div>*/}
      {/*)}*/}

      {/* Кнопки зума (ВНЕ overlay!) */}
      {zoomControls}
    </ScanAreaDiv>
  );
};

const ScanAreaDiv = styled("div")(
  ({theme}) => css`
    width: 100%;
    height: 100%;
    background-color: #000;
    position: relative;
  `,
);

const Video = styled("video")(
  ({theme}) => css`
    width: 100%;
    height: 100%;
    object-fit: cover;
    cursor: pointer; // Курсор указателя для клика (перефокусировка + зум)
    user-select: none; // Запретить выделение
    -webkit-user-drag: none; // Запретить drag
    touch-action: none; // Полностью отключить нативные жесты браузера (pinch, swipe и т.д.)
  `,
);

const CenterMessageBox = styled("div")(
  ({theme}) => css`
    position: absolute;
    top: 0;
    right: 0;
    left: 0;
    bottom: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    color: #fff;
  `,
);
