import {useEffect, useRef, useState} from "react";
import {IS_DEV} from "@/configuration/flagsConstants.ts";

// TypeScript типы для MediaStream API (zoom)
interface ZoomCapability {
  min: number;
  max: number;
  step: number;
}

interface ExtendedCapabilities extends MediaTrackCapabilities {
  zoom?: ZoomCapability;
}

interface UseCameraZoomParams {
  streamRef: React.RefObject<MediaStream | null>;
  scanAreaRef: React.RefObject<HTMLDivElement | null>;
}

interface UseCameraZoomReturn {
  zoomLevel: number;
  zoomCapabilities: {min: number; max: number; step: number; supported: boolean};
  handleZoomIn: () => void;
  handleZoomOut: () => void;
  handleWheelZoom: (e: React.WheelEvent) => void;
  applyZoom: (newZoom: number) => Promise<void>;
  initZoomCapabilities: (track: MediaStreamTrack) => void;
  zoomLevelRef: React.RefObject<number>;
  zoomCapabilitiesRef: React.RefObject<{
    min: number;
    max: number;
    step: number;
    supported: boolean;
  }>;
}

/**
 * Хук для управления зумом камеры через MediaStream API
 *
 * - Проверяет capabilities.zoom
 * - Применяет зум через track.applyConstraints()
 * - Обрабатывает pinch-to-zoom, кнопки +/−, колесо мыши
 * - Предотвращает race condition при быстрых вызовах applyConstraints()
 */
export const useCameraZoom = ({
  streamRef,
  scanAreaRef,
}: UseCameraZoomParams): UseCameraZoomReturn => {
  const [zoomLevel, setZoomLevel] = useState(1); // Текущий уровень зума (1 = без зума)
  const [zoomCapabilities, setZoomCapabilities] = useState<{
    min: number;
    max: number;
    step: number;
    supported: boolean;
  }>({min: 1, max: 1, step: 0.1, supported: false});

  // Ref для предотвращения race condition при зуме
  const applyingZoomRef = useRef(false);
  // Ref для pinch-to-zoom
  const touchStartRef = useRef<{distance: number; zoom: number} | null>(null);
  // Ref для актуальных значений зума (для touch handlers)
  const zoomLevelRef = useRef(zoomLevel);
  const zoomCapabilitiesRef = useRef(zoomCapabilities);

  // Инициализация capabilities камеры (вызывается после старта камеры)
  const initZoomCapabilities = (track: MediaStreamTrack) => {
    const capabilities = track.getCapabilities() as ExtendedCapabilities;

    // 🔍 ОТЛАДКА: Логируем zoom capabilities
    if (IS_DEV) console.log("📷 Zoom capabilities:", capabilities.zoom);

    if (capabilities.zoom) {
      if (IS_DEV) console.log("✅ Зум поддерживается:", capabilities.zoom);
      setZoomCapabilities({
        min: capabilities.zoom.min || 1,
        max: capabilities.zoom.max || 4,
        step: capabilities.zoom.step || 0.1,
        supported: true,
      });
      setZoomLevel(1); // Сброс зума при переключении камеры
    } else {
      if (IS_DEV) console.log("❌ Зум НЕ поддерживается");
      setZoomCapabilities({min: 1, max: 1, step: 0.1, supported: false});
    }
  };

  // Применение зума к камере
  const applyZoom = async (newZoom: number) => {
    // ВАЖНО: Используем ref для актуального значения capabilities (чтобы не захватить старое из замыкания)
    if (!zoomCapabilitiesRef.current.supported) {
      if (IS_DEV) console.warn("Камера не поддерживает зум");
      return;
    }

    // Предотвращаем race condition
    if (applyingZoomRef.current) return;
    applyingZoomRef.current = true;

    const track = streamRef.current?.getVideoTracks()[0];
    if (!track) {
      applyingZoomRef.current = false;
      return;
    }

    // Ограничиваем зум в пределах min/max
    const clampedZoom = Math.max(
      zoomCapabilitiesRef.current.min,
      Math.min(zoomCapabilitiesRef.current.max, newZoom),
    );

    try {
      await track.applyConstraints({
        advanced: [{zoom: clampedZoom}],
      } as never); // zoom не в стандартных типах TS
      setZoomLevel(clampedZoom);
    } catch (e) {
      if (IS_DEV) console.error("Ошибка применения зума", e);
    } finally {
      applyingZoomRef.current = false;
    }
  };

  // Обработчики кнопок +/−
  const handleZoomIn = () => {
    const newZoom = zoomLevelRef.current + zoomCapabilitiesRef.current.step * 5; // Увеличить на 5 шагов
    applyZoom(newZoom);
  };

  const handleZoomOut = () => {
    const newZoom = zoomLevelRef.current - zoomCapabilitiesRef.current.step * 5; // Уменьшить на 5 шагов
    applyZoom(newZoom);
  };

  // Обработчик колеса мыши (Ctrl + колесо)
  const handleWheelZoom = (e: React.WheelEvent) => {
    if (!e.ctrlKey) return; // Зум только с Ctrl
    e.preventDefault();

    const delta =
      e.deltaY > 0 ? -zoomCapabilitiesRef.current.step : zoomCapabilitiesRef.current.step;
    const newZoom = zoomLevelRef.current + delta;
    applyZoom(newZoom);
  };

  // Нативные touch handlers для pinch-to-zoom (будут добавлены через useEffect с {passive: false})
  const handleTouchStart = (e: TouchEvent) => {
    // ТОЛЬКО при 2 пальцах обрабатываем pinch
    // При 1 пальце НЕ трогаем событие, чтобы оно дошло до video для single/double tap
    if (e.touches.length === 2) {
      const dx = e.touches[0].clientX - e.touches[1].clientX;
      const dy = e.touches[0].clientY - e.touches[1].clientY;
      const distance = Math.sqrt(dx * dx + dy * dy);
      // Используем ref для актуального значения zoomLevel
      touchStartRef.current = {distance, zoom: zoomLevelRef.current};
    } else {
      // Сбрасываем ref если был pinch, а стал 1 палец
      touchStartRef.current = null;
    }
  };

  const handleTouchMove = (e: TouchEvent) => {
    // ВАЖНО: preventDefault только при 2 пальцах (pinch)!
    // При 1 пальце НЕ трогаем событие, чтобы use-double-tap работал на video
    if (e.touches.length === 2 && touchStartRef.current) {
      e.preventDefault(); // Предотвратить скролл только при pinch (работает с {passive: false})

      const dx = e.touches[0].clientX - e.touches[1].clientX;
      const dy = e.touches[0].clientY - e.touches[1].clientY;
      const distance = Math.sqrt(dx * dx + dy * dy);
      const scale = distance / touchStartRef.current.distance;
      const newZoom = touchStartRef.current.zoom * scale;

      applyZoom(newZoom);
    }
  };

  const handleTouchEnd = () => {
    touchStartRef.current = null;
  };

  // Синхронизируем ref'ы с state (для touch handlers)
  useEffect(() => {
    zoomLevelRef.current = zoomLevel;
  }, [zoomLevel]);

  useEffect(() => {
    zoomCapabilitiesRef.current = zoomCapabilities;
  }, [zoomCapabilities]);

  // Регистрация нативных touch handlers с {passive: false} для pinch-to-zoom
  useEffect(() => {
    const scanArea = scanAreaRef.current;
    if (!scanArea) return;

    // Добавляем нативные listeners с {passive: false}
    scanArea.addEventListener("touchstart", handleTouchStart, {passive: false});
    scanArea.addEventListener("touchmove", handleTouchMove, {passive: false});
    scanArea.addEventListener("touchend", handleTouchEnd, {passive: false});

    return () => {
      scanArea.removeEventListener("touchstart", handleTouchStart);
      scanArea.removeEventListener("touchmove", handleTouchMove);
      scanArea.removeEventListener("touchend", handleTouchEnd);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []); // Пустые deps: регистрируем handlers один раз, они используют ref для актуальных значений

  return {
    zoomLevel,
    zoomCapabilities,
    handleZoomIn,
    handleZoomOut,
    handleWheelZoom,
    applyZoom,
    initZoomCapabilities,
    zoomLevelRef,
    zoomCapabilitiesRef,
  };
};
