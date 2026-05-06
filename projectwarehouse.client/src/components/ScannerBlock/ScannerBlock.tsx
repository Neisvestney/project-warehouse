import React, {type ReactNode, useCallback, useEffect, useLayoutEffect, useRef, useState} from "react";
import {ScanArea, ZoomControls} from "./components";
import type {ViewfinderRect} from "./components";
import {IS_DEV} from "../../configuration/flagsConstants.ts";
import {useCameraFocus, useCameraStream, useCameraZoom} from "../../utils/camera";
import {
  createQrScanLoop,
  DEFAULT_SCANNER_OPTIONS,
  validateQrCode,
  type NormalizedBarcodePosition,
} from "../../utils/qrTools.ts";
import type {ReadResult} from "zxing-wasm/reader";
import {Container, css, SpeedDial, SpeedDialAction, styled} from "@mui/material";
import SpeedDialIcon from "@mui/material/SpeedDialIcon";
import SettingsIcon from "@mui/icons-material/Settings";
import CloseIcon from "@mui/icons-material/Close";
import FlipCameraIosIcon from "@mui/icons-material/FlipCameraIos";
import CameraSelectDialog from "./components/CameraSelectDialog.tsx";

export interface ScannerBlockProps {
  onScanned: (barCodeTextData: string, barcodeRawData: DetectedBarcode | ReadResult) => void;
  onError?: (error: ReactNode | string) => void;
  // true - НЕ прошла валидацию
  additionalValidation?: (qrData: string) => ReactNode | string | null | undefined | void;
  additionalValidationAsync?: (
    qrData: string,
  ) => Promise<ReactNode | string | null | undefined | void>;
  restart?: number;
}

function ScannerBlock({
  onScanned,
  onError,
  additionalValidation,
  additionalValidationAsync,
  restart,
}: ScannerBlockProps) {
  const [error, setError] = useState<ReactNode | string | null>(null);
  const [scannedLog, setScannedLog] = useState("");
  const [scanInterval, setScanInterval] = useState(100);
  const [cameraSelectDialogOpen, setCameraSelectDialogOpen] = useState(false);
  const [viewfinderRect, setViewfinderRect] = useState<ViewfinderRect | null>(null);
  const [detectedPositions, setDetectedPositions] = useState<NormalizedBarcodePosition[]>([]);
  const scanIntervalRef = useRef(100);
  const qrScannedHandlerRef =
    useRef<
      (barCodeTextData: string, barcodeRawData: DetectedBarcode | ReadResult) => Promise<boolean>
    >(null);
  const cropRegionRef = useRef<{x: number; y: number; w: number; h: number} | null>(null);
  const onBarcodePositionRef = useRef<((positions: NormalizedBarcodePosition[]) => void) | null>(null);
  const clearPositionsTimerRef = useRef<number | undefined>(undefined);
  const viewfinderRectRef = useRef<ViewfinderRect | null>(null);
  const viewfinderSizeRef = useRef<{w: number; h: number} | null>(null);
  const scanLoopCleanupRef = useRef<(() => void) | null>(null);
  // Объявляем scanAreaRef здесь чтобы он был доступен во всех хуках ниже
  const scanAreaRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    // Синхронизируем ref с state для динамического изменения
    scanIntervalRef.current = scanInterval;
  }, [scanInterval]);

  // Хуки камеры объявляем раньше, чтобы cameraStream был доступен в следующих хуках
  const cameraStream = useCameraStream({
    restart,
    onStreamReady: handleStreamReady,
  });

  const cameraZoom = useCameraZoom({
    streamRef: cameraStream.streamRef,
    scanAreaRef,
  });

  const cameraFocus = useCameraFocus({
    streamRef: cameraStream.streamRef,
    applyZoom: cameraZoom.applyZoom,
    zoomLevelRef: cameraZoom.zoomLevelRef,
    zoomCapabilitiesRef: cameraZoom.zoomCapabilitiesRef,
  });

  // Viewfinder фиксирован в центре — ResizeObserver держит его по центру при изменении размера
  useLayoutEffect(() => {
    const container = scanAreaRef.current;
    if (!container) return;

    const computeViewfinder = () => {
      const {clientWidth: dw, clientHeight: dh} = container;
      if (!dw || !dh) return;
      let w: number, h: number;
      if (viewfinderSizeRef.current) {
        w = Math.min(viewfinderSizeRef.current.w, dw * 0.97);
        h = Math.min(viewfinderSizeRef.current.h, dh * 0.97);
      } else {
        const size = Math.min(dw, dh) * 0.75;
        w = size;
        h = size;
      }
      const rect: ViewfinderRect = {x: (dw - w) / 2, y: (dh - h) / 2, w, h};
      viewfinderRectRef.current = rect;
      setViewfinderRect(rect);
    };

    computeViewfinder();
    const ro = new ResizeObserver(computeViewfinder);
    ro.observe(container);
    return () => ro.disconnect();
  }, []);

  // Перевод viewfinder (screen px) в натуральные координаты видео для scan loop
  const updateCropRegion = useCallback(
    (rect: ViewfinderRect) => {
      const video = cameraStream.videoRef.current;
      const container = scanAreaRef.current;
      const vw = video?.videoWidth ?? 0;
      const vh = video?.videoHeight ?? 0;
      const dw = container?.clientWidth ?? 0;
      const dh = container?.clientHeight ?? 0;
      if (!vw || !vh || !dw || !dh) {
        cropRegionRef.current = null;
        return;
      }
      const scale = Math.max(dw / vw, dh / vh);
      const ox = (vw * scale - dw) / 2;
      const oy = (vh * scale - dh) / 2;
      cropRegionRef.current = {
        x: (rect.x + ox) / scale,
        y: (rect.y + oy) / scale,
        w: rect.w / scale,
        h: rect.h / scale,
      };
    },
    [cameraStream.videoRef],
  );

  const handleResizeViewfinder = useCallback(
    (newW: number, newH: number) => {
      const container = scanAreaRef.current;
      if (!container) return;
      const {clientWidth: dw, clientHeight: dh} = container;
      const MIN = 80;
      const w = Math.max(MIN, Math.min(newW, dw * 0.97));
      const h = Math.max(MIN, Math.min(newH, dh * 0.97));
      viewfinderSizeRef.current = {w, h};
      const rect: ViewfinderRect = {x: (dw - w) / 2, y: (dh - h) / 2, w, h};
      viewfinderRectRef.current = rect;
      setViewfinderRect(rect);
      updateCropRegion(rect);
    },
    [updateCropRegion],
  );

  // Синхронизируем cropRegion когда camera становится активной (видео загружено)
  useEffect(() => {
    if (cameraStream.mode === "camera" && viewfinderRectRef.current) {
      updateCropRegion(viewfinderRectRef.current);
    }
  }, [cameraStream.mode, updateCropRegion]);

  // Обработчик позиций баркодов из scan loop
  useEffect(() => {
    onBarcodePositionRef.current = (positions: NormalizedBarcodePosition[]) => {
      if (positions.length > 0) {
        window.clearTimeout(clearPositionsTimerRef.current);
        setDetectedPositions(positions);
      } else {
        clearPositionsTimerRef.current = window.setTimeout(() => {
          setDetectedPositions([]);
        }, 300);
      }
    };
  }, []);

  useEffect(() => {
    return () => {
      window.clearTimeout(clearPositionsTimerRef.current);
      scanLoopCleanupRef.current?.();
    };
  }, []);

  // Колбэк после старта камеры — инициализация зума/фокуса и запуск scan loop
  // function-declaration поднимается (hoisting), поэтому передаётся в useCameraStream выше
  function handleStreamReady({track}: {track: MediaStreamTrack; video: HTMLVideoElement}) {
    cameraZoom.initZoomCapabilities(track);
    cameraFocus.initFocusMode(track);

    // Обновляем cropRegion теперь что видео готово
    if (viewfinderRectRef.current) {
      updateCropRegion(viewfinderRectRef.current);
    }

    scanLoopCleanupRef.current?.();
    scanLoopCleanupRef.current = createQrScanLoop({
      videoRef: cameraStream.videoRef,
      isScanningRef: cameraStream.isScanningRef,
      readerOptions: DEFAULT_SCANNER_OPTIONS,
      scanIntervalRef,
      onBarcodeDetected: qrScannedHandlerRef,
      cropRegionRef,
      onBarcodePosition: onBarcodePositionRef,
    });
  }

  // Обработчик сканирования. Возвращает true если нужно остановить сканирование (успех), false — продолжить (ошибка валидации)
  const handleQrScanned = useCallback(
    async (
      barCodeTextData: string,
      barcodeRawData: DetectedBarcode | ReadResult,
    ): Promise<boolean> => {
      setScannedLog(barCodeTextData);
      const validation = validateQrCode(barCodeTextData);

      if (!validation.valid) {
        const errorMsg = validation.error || "Неверный формат QR-кода";
        setError(errorMsg);
        onError?.(errorMsg);
        return false; // продолжить сканирование
      }

      if (additionalValidation) {
        const additionalError = additionalValidation(validation.data!);
        if (additionalError) {
          setError(additionalError);
          onError?.(additionalError);
          return false; // продолжить сканирование
        }
      }
      if (additionalValidationAsync) {
        const additionalError = await additionalValidationAsync(validation.data!);
        if (additionalError) {
          setError(additionalError);
          onError?.(additionalError);
          return false; // продолжить сканирование
        }
      }

      setError(null);
      // cameraStream.stopCamera();
      onScanned(validation.data!, barcodeRawData);
      return false;
    },
    [additionalValidation, additionalValidationAsync, onError, onScanned],
  );

  useEffect(() => {
    qrScannedHandlerRef.current = handleQrScanned;
  }, [handleQrScanned]);

  const scanIntervalOptions = [
    {value: 250, label: "4 FPS (250ms)"},
    {value: 125, label: "8 FPS (125ms)"},
    {value: 100, label: "10 FPS (100ms)"},
    {value: 83, label: "12 FPS (83ms)"},
    {value: 62, label: "~16 FPS (62ms)"},
    {value: 40, label: "25 FPS (40ms)"},
  ];

  // Добавьте UI для выбора камеры
  const renderCameraSelector = () => {
    if (cameraStream.availableDevices.length <= 1 || cameraStream.mode !== "camera") return null;

    return (
      <>
        {/*<SelectLabeledInput*/}
        {/*  label={"Выбор камеры"}*/}
        {/*  value={cameraStream.selectedDeviceId}*/}
        {/*  options={cameraStream.deviceOptions}*/}
        {/*  onChange={cameraStream.setSelectedDeviceId}*/}
        {/*/>*/}
      </>
    );
  };

  return (
    <ScanAreaWrapper>
      <ScanArea
        mode={cameraStream.mode}
        cameraError={cameraStream.cameraError}
        isPortrait={cameraStream.isPortrait}
        isFocusing={cameraFocus.isFocusing}
        videoRef={cameraStream.videoRef}
        scanAreaRef={scanAreaRef}
        doubleTapBind={cameraFocus.doubleTapBind}
        onWheelZoom={cameraZoom.handleWheelZoom}
        viewfinderRect={viewfinderRect}
        detectedPositions={detectedPositions}
        onResizeViewfinder={handleResizeViewfinder}
        zoomControls={
          <ZoomControls
            mode={cameraStream.mode}
            zoomLevel={cameraZoom.zoomLevel}
            zoomCapabilities={cameraZoom.zoomCapabilities}
            onZoomIn={cameraZoom.handleZoomIn}
            onZoomOut={cameraZoom.handleZoomOut}
          />
        }
      />
      <Container
        sx={{
          padding: 2,
          display: "flex",
          alignItems: "flex-end",
          justifyContent: "flex-end",
          height: "100%",
          position: "absolute",
          top: 0,
          bottom: 0,
          right: 0,
          left: 0,
          pointerEvents: "none",
        }}
      >
        <SpeedDial
          FabProps={{color: "default"}}
          icon={<SpeedDialIcon icon={<SettingsIcon />} openIcon={<CloseIcon />} />}
          ariaLabel={"SpeedDial"}
          sx={{pointerEvents: "auto"}}
        >
          {cameraStream.availableDevices.length > 0 && cameraStream.mode == "camera" && (
            <SpeedDialAction
              sx={{pointerEvents: "auto"}}
              icon={<FlipCameraIosIcon />}
              slotProps={{
                fab: {
                  onClick: () => {
                    setCameraSelectDialogOpen(true);
                  },
                },
                tooltip: {
                  open: true,
                  title: "Выбрать камеру",
                },
              }}
            />
          )}
        </SpeedDial>
      </Container>
      <CameraSelectDialog
        open={cameraSelectDialogOpen}
        setOpen={setCameraSelectDialogOpen}
        selectDeviceId={cameraStream.setSelectedDeviceId}
        devicesOptions={cameraStream.deviceOptions}
        selectedDeviceId={cameraStream.selectedDeviceId}
      />

      {/*{IS_DEV && scannedLog && <div>DEV LOG scanned: {scannedLog}</div>}*/}
      {/*{error && <div>{error}</div>}*/}

      {/*{IS_DEV && <div className={cn(styles.actions)}>*/}
      {/*    <SelectLabeledInput*/}
      {/*      label="Частота сканирования (DEV)"*/}
      {/*      value={scanInterval}*/}
      {/*      options={scanIntervalOptions}*/}
      {/*      onChange={setScanInterval}*/}
      {/*    />*/}
      {/*</div>}*/}
    </ScanAreaWrapper>
  );
}

export default ScannerBlock;

const ScanAreaWrapper = styled("div")(
  ({theme}) => css`
    width: 100%;
    height: 100%;
    position: relative;
    overflow: hidden;
  `,
);
