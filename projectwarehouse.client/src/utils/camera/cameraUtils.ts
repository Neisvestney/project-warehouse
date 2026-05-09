/**
 * Утилиты для работы с камерой (без зависимостей от zxing-wasm)
 * Этот файл может импортироваться отдельно, не загружая тяжелые библиотеки сканирования
 */

/**
 * Проверяет доступность камеры в браузере
 * @returns Promise с результатом проверки
 */
export const checkCameraAvailability = async (): Promise<{
  available: boolean;
  error?: string;
}> => {
  // Проверяем поддержку API
  if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
    return {
      available: false,
      error: "Ваш браузер не поддерживает доступ к камере",
    };
  }

  try {
    // Проверяем наличие камер
    const devices = await navigator.mediaDevices.enumerateDevices();
    const hasCamera = devices.some((device) => device.kind === "videoinput");

    if (!hasCamera) {
      return {available: false, error: "Камера не найдена на устройстве"};
    }

    return {available: true};
  } catch {
    return {
      available: false,
      error: "Не удалось проверить доступность камеры",
    };
  }
};

/**
 * Определяет, является ли устройство мобильным
 */
export const isMobileDevice = (): boolean => {
  return /iPhone|iPad|iPod|Android/i.test(navigator.userAgent);
};
