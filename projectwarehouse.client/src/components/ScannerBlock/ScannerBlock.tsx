import React, {type ReactNode, useCallback, useEffect, useRef, useState} from "react";
import {ScanArea, ZoomControls} from "./components";
import {IS_DEV} from "../../configuration/flagsConstants.ts";
import {useCameraFocus, useCameraStream, useCameraZoom} from "../../utils/camera";
import {createQrScanLoop, DEFAULT_READER_OPTIONS, validateQrCode} from "../../utils/qrTools.ts";
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
  const scanIntervalRef = useRef(100);
  const qrScannedHandlerRef =
    useRef<
      (barCodeTextData: string, barcodeRawData: DetectedBarcode | ReadResult) => Promise<boolean>
    >(null);

  useEffect(() => {
    // Синхронизируем ref с state для динамического изменения
    scanIntervalRef.current = scanInterval;
  }, [scanInterval]);

  const scanAreaRef = useRef<HTMLDivElement>(null);

  // Хуки камеры
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

  // Колбэк после старта камеры — инициализация зума/фокуса и запуск scan loop
  function handleStreamReady({track}: {track: MediaStreamTrack; video: HTMLVideoElement}) {
    // Инициализируем capabilities зума и фокуса
    cameraZoom.initZoomCapabilities(track);
    cameraFocus.initFocusMode(track);

    // Запускаем scan loop
    createQrScanLoop({
      videoRef: cameraStream.videoRef,
      isScanningRef: cameraStream.isScanningRef,
      readerOptions: DEFAULT_READER_OPTIONS,
      scanIntervalRef,
      onBarcodeDetected: qrScannedHandlerRef,
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
              onClick={() => {
                console.log("AAAAAAA");
              }}
              slotProps={{
                fab: {
                  onClick: () => {
                    console.log("AAAAAAA");
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
