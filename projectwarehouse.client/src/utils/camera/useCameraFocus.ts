import {useState} from "react";
import {useDoubleTap} from "use-double-tap";
import {IS_DEV} from "../../configuration/flagsConstants.ts";

// TypeScript типы для MediaStream API (focus)
interface ExtendedCapabilities extends MediaTrackCapabilities {
  focusMode?: string[];
}

interface UseCameraFocusParams {
  streamRef: React.RefObject<MediaStream | null>;
  applyZoom: (newZoom: number) => Promise<void>;
  zoomLevelRef: React.RefObject<number>;
  zoomCapabilitiesRef: React.RefObject<{
    min: number;
    max: number;
    step: number;
    supported: boolean;
  }>;
}

interface UseCameraFocusReturn {
  isFocusing: boolean;
  doubleTapBind: ReturnType<typeof useDoubleTap>;
  initFocusMode: (track: MediaStreamTrack) => Promise<void>;
}

/**
 * Хук для управления фокусировкой камеры через MediaStream API
 *
 * - Continuous autofocus по умолчанию
 * - Перефокусировка по клику/тапу (single-shot → continuous)
 * - Double tap для быстрого зума 2x
 * - Визуальная индикация фокусировки
 */
export const useCameraFocus = ({
  streamRef,
  applyZoom,
  zoomLevelRef,
  zoomCapabilitiesRef,
}: UseCameraFocusParams): UseCameraFocusReturn => {
  const [isFocusing, setIsFocusing] = useState(false);

  // Инициализация continuous autofocus (вызывается после старта камеры)
  const initFocusMode = async (track: MediaStreamTrack) => {
    const capabilities = track.getCapabilities() as ExtendedCapabilities;

    if (capabilities.focusMode) {
      if (IS_DEV)
        console.log("✅ FocusMode поддерживается. Доступные режимы:", capabilities.focusMode);
      try {
        await track.applyConstraints({
          advanced: [{focusMode: "continuous"}],
        } as never); // Type casting: focusMode не в стандартных типах TS
        if (IS_DEV) console.log("✅ Continuous autofocus успешно применён");
      } catch (e) {
        if (IS_DEV) console.warn("❌ Ошибка применения continuous autofocus:", e);
      }
    } else {
      if (IS_DEV) console.log("❌ FocusMode НЕ поддерживается камерой");
    }
  };

  // Single tap для перефокусировки
  const handleRefocus = async () => {
    if (IS_DEV) console.log("🎯 handleRefocus вызван!");
    const track = streamRef.current?.getVideoTracks()[0];
    if (!track) {
      if (IS_DEV) console.warn("❌ handleRefocus: track отсутствует");
      return;
    }

    // Проверяем capabilities перед применением
    const capabilities = track.getCapabilities() as ExtendedCapabilities;
    if (!capabilities.focusMode) {
      if (IS_DEV) console.warn("❌ handleRefocus: FocusMode не поддерживается");
      return;
    }

    try {
      setIsFocusing(true);
      if (IS_DEV) console.log("🔵 Применяем single-shot focus...");

      // 1. Одноразовая фокусировка
      await track.applyConstraints({
        advanced: [{focusMode: "single-shot"}],
      } as never); // Type casting: focusMode не в стандартных типах TS
      if (IS_DEV) console.log("✅ Single-shot focus применён");

      // 2. Через 100ms обратно в continuous
      setTimeout(async () => {
        if (IS_DEV) console.log("🔄 Возвращаемся в continuous autofocus...");
        try {
          await track.applyConstraints({
            advanced: [{focusMode: "continuous"}],
          } as never);
          if (IS_DEV) console.log("✅ Continuous autofocus восстановлен");
        } catch (e) {
          if (IS_DEV) console.warn("❌ Не удалось вернуться в continuous autofocus:", e);
        }
        setIsFocusing(false);
      }, 100);
    } catch (e) {
      if (IS_DEV) console.warn("❌ Ошибка перефокусировки:", e);
      setIsFocusing(false);
    }
  };

  // Double tap для быстрого зума 2x
  const handleDoubleTap = async () => {
    if (IS_DEV)
      console.log(
        "👆👆 handleDoubleTap вызван! Зум поддерживается:",
        zoomCapabilitiesRef.current?.supported,
      );
    if (!zoomCapabilitiesRef.current?.supported || !zoomLevelRef.current) return;

    if (zoomLevelRef.current <= zoomCapabilitiesRef.current.min + 0.1) {
      // Зумим в 2x (или до max, если max < 2)
      const targetZoom = Math.min(2, zoomCapabilitiesRef.current.max);
      if (IS_DEV) console.log(`🔍 Зумим в ${targetZoom}x`);
      await applyZoom(targetZoom);
    } else {
      // Возврат к 1x
      if (IS_DEV) console.log("🔍 Сбрасываем зум в 1x");
      await applyZoom(zoomCapabilitiesRef.current.min);
    }
  };

  // Привязка double tap и single tap через use-double-tap
  const doubleTapBind = useDoubleTap(
    handleDoubleTap, // onDoubleTap
    300, // threshold (300ms)
    {
      onSingleTap: handleRefocus, // Single tap → перефокусировка
    },
  );

  return {
    isFocusing,
    doubleTapBind,
    initFocusMode,
  };
};
