// ВАЖНО: Этот модуль загружается динамически через React.lazy в OrderAssemblyScanQrModal
// Статические импорты zxing-wasm здесь допустимы, т.к. весь модуль попадёт в отдельный chunk
import React from "react";
import {readBarcodes} from "zxing-wasm/reader";
import type {ReaderOptions, ReadResult} from "zxing-wasm/reader";

// Импортируем WASM как URL через Vite — работает и в dev, и в prod
import zxingWasmUrl from "zxing-wasm/reader/zxing_reader.wasm?url";
import {IS_DEV} from "../configuration/flagsConstants";
import {prepareZXingModule} from "zxing-wasm";

// Настраиваем локальную загрузку WASM
if (IS_DEV) console.log("[ZXing] WASM URL:", zxingWasmUrl);
prepareZXingModule({
  overrides: {
    locateFile: (path: string, prefix: string) => {
      if (path.endsWith(".wasm")) {
        if (IS_DEV) console.log("[ZXing] Loading WASM from:", zxingWasmUrl);
        return zxingWasmUrl;
      }
      return prefix + path;
    },
  },
  equalityFn: Object.is,
  fireImmediately: false,
});

/**
 * Конфигурация для zxing-wasm reader
 * Эквивалент hints из BrowserQRCodeReader
 */
export const DEFAULT_READER_OPTIONS: ReaderOptions = {
  formats: ["DataMatrix", "EAN13"], // MicroQRCode для мелких кодов с одним квадратом!
  tryHarder: true,
  maxNumberOfSymbols: 1,
};

const BARCODE_DETECTOR_API_FORMATS: BarcodeFormat[] = ["data_matrix", "ean_13"];

/**
 * Валидирует QR-код по заданным критериям
 * @param qrData - строка с данными QR-кода
 * @param minLength - минимальная длина (по умолчанию 4)
 * @param maxLength - максимальная длина (по умолчанию 30)
 * @returns объект с результатом валидации
 */
export const validateQrCode = (qrData: string): {valid: boolean; error?: string; data?: string} => {
  const trimmedData = qrData.trim();

  if (!trimmedData) {
    return {valid: false, error: "Код пустой"};
  }

  return {valid: true, data: trimmedData};
};

/**
 * Декодирует QR-код из загруженного файла изображения
 * Использует цепочку: BarcodeDetector (если доступен) → zxing-wasm
 */
export const decodeQrFromImage = async (
  file: File,
): Promise<{success: boolean; data?: string; error?: string}> => {
  if (!file.type.startsWith("image/")) {
    return {success: false, error: "Выберите файл изображения"};
  }

  try {
    // 1. BarcodeDetector (если доступен)
    const hasBarcodeDetector =
      typeof window !== "undefined" && typeof BarcodeDetector !== "undefined";
    if (hasBarcodeDetector) {
      try {
        const detector = new BarcodeDetector!({formats: BARCODE_DETECTOR_API_FORMATS});
        const barcodes = await detector.detect(file);
        const qr = barcodes?.[0];
        if (qr?.rawValue) {
          const validation = validateQrCode(qr.rawValue);
          if (validation.valid) {
            return {success: true, data: validation.data};
          }
          return {success: false, error: validation.error};
        }
      } catch {
        // Fallback to zxing-wasm
      }
    }

    // 2. zxing-wasm (работает напрямую с File)
    try {
      const results = await readBarcodes(file, DEFAULT_READER_OPTIONS);
      const text = results[0]?.text;

      if (!text) {
        return {
          success: false,
          error: "QR-код не найден на изображении. Попробуйте другое фото",
        };
      }

      const validation = validateQrCode(text);
      if (validation.valid) {
        return {success: true, data: validation.data};
      } else {
        return {success: false, error: validation.error};
      }
    } catch {
      return {
        success: false,
        error: "QR-код не найден на изображении. Попробуйте другое фото",
      };
    }
  } catch (e) {
    if (IS_DEV) console.error("Ошибка при декодировании изображения", e);
    return {
      success: false,
      error: "Ошибка при декодировании изображения",
    };
  }
};

/**
 * Вычисляет оптимальный порог бинаризации методом Otsu (с сэмплированием для производительности)
 * Хорошо работает с QR-кодами на бежевом/деревянном фоне
 */
export const otsuThreshold = (imageData: ImageData): number => {
  const {data} = imageData;
  const histogram = new Array(256).fill(0);
  const step = 16; // Каждый 4-й пиксель (4 × 4 RGBA) — быстрее в ~4 раза

  let sampleCount = 0;
  for (let i = 0; i < data.length; i += step) {
    const gray = Math.round(data[i] * 0.299 + data[i + 1] * 0.587 + data[i + 2] * 0.114);
    histogram[gray]++;
    sampleCount++;
  }

  let sum = 0;
  for (let i = 0; i < 256; i++) sum += i * histogram[i];

  let sumB = 0,
    wB = 0,
    wF = 0;
  let maxVariance = 0,
    threshold = 128;

  for (let t = 0; t < 256; t++) {
    wB += histogram[t];
    if (wB === 0) continue;

    wF = sampleCount - wB;
    if (wF === 0) break;

    sumB += t * histogram[t];
    const mB = sumB / wB;
    const mF = (sum - sumB) / wF;

    const variance = wB * wF * (mB - mF) * (mB - mF);
    if (variance > maxVariance) {
      maxVariance = variance;
      threshold = t;
    }
  }

  return threshold;
};

/**
 * Применяет бинаризацию к ImageData (in-place)
 * Превращает изображение в чёрно-белое для лучшего распознавания QR
 */
export const applyBinarization = (imageData: ImageData, threshold: number): void => {
  const {data} = imageData;

  for (let i = 0; i < data.length; i += 4) {
    const gray = data[i] * 0.299 + data[i + 1] * 0.587 + data[i + 2] * 0.114;
    const bw = gray > threshold ? 255 : 0;
    data[i] = data[i + 1] = data[i + 2] = bw;
  }
};

/**
 * Preprocessing изображения для улучшения распознавания QR-кодов
 * Применяет бинаризацию с автоматическим порогом Otsu
 */
export const preprocessForQr = (
  ctx: CanvasRenderingContext2D,
  width: number,
  height: number,
): void => {
  const imageData = ctx.getImageData(0, 0, width, height);
  const threshold = otsuThreshold(imageData);
  applyBinarization(imageData, threshold);
  ctx.putImageData(imageData, 0, 0);
};

/**
 * Инвертирует цвета на canvas (для распознавания белых QR на чёрном фоне)
 */
export const invertCanvas = (
  ctx: CanvasRenderingContext2D,
  width: number,
  height: number,
): void => {
  const imageData = ctx.getImageData(0, 0, width, height);
  const {data} = imageData;

  for (let i = 0; i < data.length; i += 4) {
    data[i] = 255 - data[i]; // R
    data[i + 1] = 255 - data[i + 1]; // G
    data[i + 2] = 255 - data[i + 2]; // B
    // Alpha остаётся
  }

  ctx.putImageData(imageData, 0, 0);
};

// === QR Scan Loop ===

interface QrScanLoopParams {
  videoRef: React.RefObject<HTMLVideoElement | null>;
  isScanningRef: React.RefObject<boolean>;
  readerOptions: ReaderOptions;
  /** Ref для динамического изменения интервала сканирования */
  scanIntervalRef: React.RefObject<number>;
  /** Колбэк при обнаружении QR. Возвращает true для остановки сканирования, false для продолжения */
  onBarcodeDetected: React.RefObject<
    | ((barCodeTextData: string, barcodeRawData: DetectedBarcode | ReadResult) => Promise<boolean>)
    | null
  >;
}

/**
 * Создаёт и запускает цикл сканирования QR-кодов из видеопотока
 *
 * - Использует BarcodeDetector (если доступен) → zxing-wasm fallback → инвертированное изображение
 * - Обрабатывает кадры с заданным интервалом
 * - Обрезает видео до квадрата 1:1 (центральная часть)
 * - Применяет preprocessing (бинаризация Otsu)
 *
 * @returns cleanup-функция для остановки цикла
 */
export const createQrScanLoop = ({
  videoRef,
  isScanningRef,
  readerOptions,
  scanIntervalRef,
  onBarcodeDetected,
}: QrScanLoopParams): (() => void) => {
  let timeoutId: number | undefined;

  // const hasBarcodeDetector = typeof BarcodeDetector !== "undefined";
  const hasBarcodeDetector = false; // TODO enable
  const detector = hasBarcodeDetector
    ? new BarcodeDetector!({formats: BARCODE_DETECTOR_API_FORMATS})
    : null;

  let frameCount = 0;
  if (IS_DEV) console.log("[ZXing] Scan loop created, detector:", !!detector);

  const loop = async () => {
    const continueLoop = () => {
      if (isScanningRef.current) {
        timeoutId = window.setTimeout(loop, scanIntervalRef.current ?? 100);
      }
    };

    frameCount++;
    // Логируем каждый первый кадр и потом каждый 30-й
    if (frameCount === 1 || frameCount % 30 === 0) {
      if (IS_DEV)
        console.log(
          "[ZXing] Loop frame:",
          frameCount,
          "scanning:",
          isScanningRef.current,
          "video:",
          !!videoRef.current,
        );
    }
    if (!isScanningRef.current || !videoRef.current) {
      if (frameCount === 1) {
        if (IS_DEV)
          console.log(
            "[ZXing] Loop exit early - scanning:",
            isScanningRef.current,
            "video:",
            !!videoRef.current,
          );
      }
      return;
    }

    try {
      const video = videoRef.current;
      if (video.readyState < 2) {
        if (frameCount === 1) {
          if (IS_DEV) console.log("[ZXing] Video not ready, readyState:", video.readyState);
        }
        setTimeout(loop, scanIntervalRef.current ?? 100);
        return;
      }

      const width = video.videoWidth || video.clientWidth;
      const height = video.videoHeight || video.clientHeight;
      if (!width || !height) {
        if (frameCount === 1) {
          if (IS_DEV) console.log("[ZXing] Video has no dimensions:", width, "x", height);
        }
        setTimeout(loop, scanIntervalRef.current ?? 100);
        return;
      }

      if (frameCount === 1) {
        if (IS_DEV)
          console.log(
            "[ZXing] Video ready! Size:",
            width,
            "x",
            height,
            "readyState:",
            video.readyState,
          );
      }

      // Вычисляем crop параметры
      const cropSize = Math.min(width, height);
      const offsetX = (width - cropSize) / 2;
      const offsetY = (height - cropSize) / 2;

      // 1. BarcodeDetector (если доступен) - работает напрямую с video
      if (detector) {
        try {
          const barcodes = await detector.detect(video);
          if (frameCount % 30 === 0) {
            if (IS_DEV) console.log("[ZXing] BarcodeDetector results:", barcodes.length);
          }

          if (barcodes.length > 0) {
            for (const barcode of barcodes) {
              if (barcode?.rawValue) {
                if (IS_DEV) console.log("[ZXing] BarcodeDetector found QR:", barcode.rawValue);
                const shouldStop = await onBarcodeDetected.current?.(barcode.rawValue, barcode);
                if (shouldStop) return;
              }
            }
            return continueLoop();
          }
        } catch (e) {
          if (IS_DEV) console.warn("[ZXing] BarcodeDetector error:", e);
        }
      }

      // 2. Fallback: zxing-wasm с preprocessing
      const shouldRunZxing = !detector || frameCount % 2 === 0;
      if (shouldRunZxing) {
        let bitmap: ImageBitmap | null = null;
        const tempCanvas = document.createElement("canvas");
        const tempCtx = tempCanvas.getContext("2d");

        if (!tempCtx) {
          setTimeout(() => loop(), scanIntervalRef.current ?? 100);
          return;
        }

        try {
          // Создаём bitmap с кроппингом (GPU-friendly)
          bitmap = await createImageBitmap(video, offsetX, offsetY, cropSize, cropSize);

          // Переносим на временный canvas для preprocessing
          tempCanvas.width = cropSize;
          tempCanvas.height = cropSize;
          tempCtx.drawImage(bitmap, 0, 0);
          bitmap.close(); // Освобождаем память
          bitmap = null;

          // Применяем preprocessing (бинаризация Otsu)
          preprocessForQr(tempCtx, cropSize, cropSize);

          // Декодируем
          const imageData = tempCtx.getImageData(0, 0, cropSize, cropSize);
          const results = await readBarcodes(imageData, readerOptions);
          if (IS_DEV && results.length > 0) console.log("[ZXing] readBarcodes results:", results);

          if (results.length > 0) {
            for (const result of results) {
              if (result.text) {
                if (IS_DEV) console.log("[ZXing] zxing-wasm found QR:", result.text);
                const shouldStop = await onBarcodeDetected.current?.(result.text, result);
                if (shouldStop) return;
              }
            }

            return continueLoop();
          }
        } catch (e) {
          if (frameCount % 30 === 0) {
            if (IS_DEV) console.warn("[ZXing] zxing-wasm error:", e);
          }
        } finally {
          if (bitmap) bitmap.close(); // Cleanup на случай ошибки
        }
      }

      // 3. Инвертированное изображение (аналогично п.2, но с invertCanvas)
      const shouldRunInverted = !detector || frameCount % 3 === 0;
      if (shouldRunInverted) {
        let bitmap: ImageBitmap | null = null;
        const tempCanvas = document.createElement("canvas");
        const tempCtx = tempCanvas.getContext("2d");

        if (!tempCtx) {
          setTimeout(() => loop(), scanIntervalRef.current ?? 100);
          return;
        }

        try {
          bitmap = await createImageBitmap(video, offsetX, offsetY, cropSize, cropSize);
          tempCanvas.width = cropSize;
          tempCanvas.height = cropSize;
          tempCtx.drawImage(bitmap, 0, 0);
          bitmap.close();
          bitmap = null;

          // Сначала preprocessing, потом инверсия
          preprocessForQr(tempCtx, cropSize, cropSize);
          invertCanvas(tempCtx, cropSize, cropSize);

          const imageData = tempCtx.getImageData(0, 0, cropSize, cropSize);
          const results = await readBarcodes(imageData, readerOptions);
          const text = results[0]?.text;

          if (text) {
            if (IS_DEV) console.log("[ZXing] zxing-wasm (inverted) found QR:", text);
            const shouldStop = await onBarcodeDetected.current?.(text, results[0]);
            if (shouldStop) return;
            else return continueLoop();
          }
        } catch (e) {
          if (frameCount % 30 === 0) {
            if (IS_DEV) console.warn("[ZXing] zxing-wasm (inverted) error:", e);
          }
        } finally {
          if (bitmap) bitmap.close();
        }
      }
    } catch (e) {
      if (IS_DEV) console.warn("[ZXing] Ошибка при попытке сканирования кадра:", e);
    }

    continueLoop();
  };

  // Запускаем цикл
  loop();

  // Cleanup-функция для остановки
  return () => {
    if (timeoutId) {
      clearTimeout(timeoutId);
    }
  };
};
