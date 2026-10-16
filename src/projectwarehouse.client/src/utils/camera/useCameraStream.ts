import {useCallback, useEffect, useMemo, useRef, useState} from "react";
import {checkCameraAvailability} from "./cameraUtils";

type ScanMode = "loading" | "camera" | "upload";

const CAMERA_STORAGE_KEY = "qr-scanner-preferred-camera";

interface UseCameraStreamParams {
  restart?: number;
  /** Колбэк после успешного получения stream — для инициализации зума/фокуса и запуска скан-луп */
  onStreamReady: (params: {track: MediaStreamTrack; video: HTMLVideoElement}) => void;
}

interface UseCameraStreamReturn {
  mode: ScanMode;
  cameraError: string | null;
  availableDevices: MediaDeviceInfo[];
  selectedDeviceId: string | undefined;
  setSelectedDeviceId: (id: string) => void;
  isPortrait: boolean;
  videoRef: React.RefObject<HTMLVideoElement | null>;
  streamRef: React.RefObject<MediaStream | null>;
  isScanningRef: React.RefObject<boolean>;
  stopCamera: () => void;
  deviceOptions: {value: string; label: string}[];
}

/**
 * Хук для управления жизненным циклом камеры
 *
 * - Permission flow
 * - Device enumeration
 * - Stream setup с constraints
 * - Определение ориентации видео (portrait/landscape)
 * - Выбор камеры (передняя/задняя)
 */
export const useCameraStream = ({
  restart,
  onStreamReady,
}: UseCameraStreamParams): UseCameraStreamReturn => {
  const [mode, setMode] = useState<ScanMode>("loading");
  const [cameraError, setCameraError] = useState<string | null>(null);
  const [availableDevices, setAvailableDevices] = useState<MediaDeviceInfo[]>([]);
  const [selectedDeviceId, setSelectedDeviceId] = useState<string | undefined>();
  const [isPortrait, setIsPortrait] = useState(false);

  const videoRef = useRef<HTMLVideoElement>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const isScanningRef = useRef(false);

  const deviceOptions = useMemo(
    () =>
      availableDevices.map((x) => ({
        value: x.deviceId,
        label: x.label,
      })),
    [availableDevices],
  );

  // Сохранение выбора камеры в localStorage (только при ручном переключении)
  const handleDeviceChange = useCallback((deviceId: string) => {
    localStorage.setItem(CAMERA_STORAGE_KEY, deviceId);
    setSelectedDeviceId(deviceId);
  }, []);

  // Остановка камеры
  const stopCamera = () => {
    isScanningRef.current = false;

    if (streamRef.current) {
      streamRef.current.getTracks().forEach((t) => t.stop());
      streamRef.current = null;
    }
    if (videoRef.current) {
      videoRef.current.srcObject = null;
    }
  };

  useEffect(() => {
    const initCamera = async () => {
      setCameraError(null);
      setMode("loading");

      const cameraCheck = await checkCameraAvailability();
      if (!cameraCheck.available) {
        setCameraError(cameraCheck.error || "Камера недоступна");
        setMode("upload");
        return;
      }

      if (!videoRef.current) return;

      try {
        // Проверяем, нужно ли запрашивать разрешение (первый запуск)
        const needsPermission = selectedDeviceId === undefined;
        let videoDevices: MediaDeviceInfo[] = [];

        if (needsPermission) {
          // 1. Запрашиваем разрешение с facingMode: environment (браузер попытается выбрать заднюю камеру)
          let tempStream: MediaStream | null = null;
          try {
            tempStream = await navigator.mediaDevices.getUserMedia({
              video: {facingMode: {ideal: "environment"}}, // да так называется задняя :)
              audio: false,
            });
          } catch (e) {
            console.error("Ошибка получения разрешения на камеру", e);
            setCameraError("Не удалось получить доступ к камере");
            setMode("upload");
            return;
          }

          // 2. Получаем deviceId камеры, которую браузер выбрал по facingMode
          const tempTrack = tempStream.getVideoTracks()[0]; // Почему-то выбирает ширик на s24 fe
          const facingModeDeviceId = tempTrack.getSettings().deviceId;

          // 3. Теперь можем получить полную информацию об устройствах
          const devices = await navigator.mediaDevices.enumerateDevices();
          videoDevices = devices.filter((d) => d.kind === "videoinput");

          // 4. Останавливаем временный stream
          tempStream.getTracks().forEach((t) => t.stop());

          if (videoDevices.length > 0) {
            console.log("Доступные камеры:", videoDevices);
            setAvailableDevices(videoDevices);

            // Приоритет выбора камеры:
            // 1. localStorage (если камера есть в списке доступных)
            // 2. facingMode: environment (выбрана браузером)
            // 3. Поиск по ключевым словам (back, rear, environment)
            const savedDeviceId = localStorage.getItem(CAMERA_STORAGE_KEY);
            const savedDeviceExists =
              savedDeviceId && videoDevices.some((d) => d.deviceId === savedDeviceId);

            let newDeviceId: string;
            if (savedDeviceExists) {
              newDeviceId = savedDeviceId;
            } else if (facingModeDeviceId) {
              newDeviceId = facingModeDeviceId;
            } else {
              const mainBackCamera = videoDevices.find((d) =>
                d.label.toLowerCase().includes("camera 0, facing back"),
              );

              const backCamera = videoDevices.find(
                (d) =>
                  d.label.toLowerCase().includes("back") ||
                  d.label.toLowerCase().includes("rear") ||
                  d.label.toLowerCase().includes("environment"),
              );
              newDeviceId =
                mainBackCamera?.deviceId || backCamera?.deviceId || videoDevices[0].deviceId;

              console.log(
                "Выбрана камера по умолчанию:",
                mainBackCamera || backCamera || videoDevices[0],
              );
            }

            setSelectedDeviceId(newDeviceId);
            // return, чтобы дождаться перезапуска useEffect с новым selectedDeviceId
            // Это предотвращает race condition: два параллельных запроса к камере
            return;
          }
        }

        // 4. Формируем constraints с учетом выбранного deviceId
        const deviceIdToUse = selectedDeviceId || videoDevices[0]?.deviceId;
        const constraints: MediaStreamConstraints = {
          video: deviceIdToUse
            ? {
                deviceId: {exact: deviceIdToUse},
                width: {ideal: 1280, max: 1920},
                height: {ideal: 720, max: 1080},
                frameRate: {ideal: 24, max: 30},
              }
            : {
                facingMode: {ideal: "environment"},
                width: {ideal: 1280, max: 1920},
                height: {ideal: 720, max: 1080},
                frameRate: {ideal: 24, max: 30},
              },
          audio: false,
        };

        // Небольшая задержка чтобы браузер освободил камеру после предыдущего stopCamera
        await new Promise((resolve) => setTimeout(resolve, 150));

        const stream = await navigator.mediaDevices.getUserMedia(constraints);
        streamRef.current = stream;
        videoRef.current.srcObject = stream;

        // Ждём старта видео
        const playPromise = videoRef.current.play();
        if (playPromise) {
          await playPromise.catch((e) => console.log("[QR] play() error:", e));
        }

        // Определяем ориентацию видео (portrait если height > width)
        const videoWidth = videoRef.current.videoWidth;
        const videoHeight = videoRef.current.videoHeight;
        setIsPortrait(videoHeight > videoWidth);

        setMode("camera");
        isScanningRef.current = true; // Включаем флаг сканирования

        // Вызываем колбэк для инициализации зума/фокуса и запуска scan loop
        const track = stream.getVideoTracks()[0];
        onStreamReady({track, video: videoRef.current});
      } catch (e) {
        console.error("Ошибка инициализации камеры", e);
        setCameraError("Не удалось получить доступ к камере");
        setMode("upload");
        stopCamera();
      }
    };

    // Первый запуск и перезапуск по restart
    initCamera();

    return () => {
      stopCamera();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [restart, selectedDeviceId]);

  return {
    mode,
    cameraError,
    availableDevices,
    selectedDeviceId,
    setSelectedDeviceId: handleDeviceChange,
    isPortrait,
    videoRef,
    streamRef,
    isScanningRef,
    stopCamera,
    deviceOptions,
  };
};
