# Нативный клиент (Android / ТСД)

Capacitor-обёртка над веб-приложением для работы на ТСД АТОЛ Smart Slim (Android 7).

## Архитектура

```
APK (локальный бандл)
  └─► main.tsx — ранний health check
        ├─ Сервер доступен → window.location.href = serverUrl (WebView переходит на сервер)
        │    └─ Весь UI и API грузятся с сервера (авто-обновления без пересборки APK)
        └─ Сервер недоступен / не выбран → ServerSetupPage (выбор сервера)
```

После перехода на сервер приложение работает полностью с него. Обновление фронтенда не требует пересборки APK.

---

## Сборка APK

```bash
cd projectwarehouse.client

# 1. Сборка фронта
npm run build

# 2. Синхронизация с Android проектом
npx cap sync

# 3. Открыть в Android Studio
npx cap open android
```

В Android Studio: **Build → Generate Signed APK** или запуск на подключённом устройстве через **Run**.

---

## Добавить предустановленный сервер

Редактировать `src/configuration/servers.ts`:

```ts
export const PREDEFINED_SERVERS: ServerConfig[] = [
  {name: "Основной склад", url: "http://192.168.1.100:7095"},
  {name: "Склад №2",       url: "http://192.168.1.101:7095"},
];
```

После изменения — пересобрать APK (`npm run build && npx cap sync`).

---

## Выбор сервера в приложении

При первом запуске открывается **ServerSetupPage**:
- Показывает предустановленные серверы (из `servers.ts`) и добавленные пользователем
- Клик по серверу — проверяет доступность (`/health`) и редиректит
- Кнопка "Добавить" — ввести имя и адрес вручную (сохраняется в localStorage)

**Смена сервера:** на странице `/login` есть иконка 🖥 в правом верхнем углу → очищает выбор и возвращает в лаунчер.

---

## Ограничения WebView

**PDF не рендерится.** WebView Android 7 на АТОЛ Smart Slim не отображает PDF ни в `<iframe>`, ни в `<object>` —
показывает пустую рамку без какого-либо события ошибки. Поэтому `PdfFileRenderer` проверяет
`Capacitor.isNativePlatform()` и вместо просмотрщика подставляет `UnsupportedFileRenderer` с кнопкой скачивания,
которая передаёт файл системному приложению.

Изображения в WebView работают штатно, включая жесты масштабирования, перетаскивание и поворот в
`FileViewerModal`.

Прочие оговорки по целевому браузеру (`chrome >= 49`) — в [frontend.md](frontend.md).

---

## Плагин аппаратного сканера АТОЛ E3

### Настройка на устройстве

1. Открыть **Barcode Utility** на ТСД
2. **Scan Setting → Data Receive Method → BROADCAST_EVENT**
3. Сохранить

### Параметры плагина

Файл: `android/app/src/main/java/app/projectwarehouse/client/AtolScannerPlugin.java`

| Константа | Значение по умолчанию | Описание |
|---|---|---|
| `SCAN_ACTION` | `android.intent.action.DECODE_DATA` | Intent action от сканера |
| `SCAN_DATA_KEY` | `barcode_string` | Ключ extra с данными штрихкода |

> Точные значения уточнить из **E3 Scanner SDK** (скачать с `fs.atol.ru` → SDK → E3 Scanner SDK).

### Использование в JS

```ts
import AtolScanner from "@/plugins/atolScanner";
import {Capacitor} from "@capacitor/core";

if (Capacitor.isNativePlatform()) {
  await AtolScanner.startListening();
  await AtolScanner.addListener("scanResult", ({barcode}) => {
    console.log("Scanned:", barcode);
  });
}
```

### Адаптация под другой ТСД

Изменить `SCAN_ACTION` и `SCAN_DATA_KEY` в `AtolScannerPlugin.java` под broadcast параметры конкретного устройства. Примеры:

| Устройство | Action | Extra key |
|---|---|---|
| АТОЛ E3 | `android.intent.action.DECODE_DATA` | `barcode_string` |
| Urovo | `android.intent.ACTION_DECODE_DATA` | `barcode_string` |
| Honeywell | `com.honeywell.aidc.action.ACTION_CLAIM_SCANNER` | — (другой API) |
| Sunmi | `com.sunmi.scanner.ACTION_DATA_CODE_RECEIVED` | `data` |
