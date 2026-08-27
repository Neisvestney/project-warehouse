import {Capacitor} from "@capacitor/core";

/**
 * Hands a generated file to the browser as a save. It is the only thing that works on native: the
 * WebView cannot render a PDF inline, so the file goes to the system viewer (docs/native-client.md).
 */
export function saveBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = fileName;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  setTimeout(() => URL.revokeObjectURL(url), 0);
}

/** `labels.pdf` → `labels-2026-08-28_14-05-09.pdf`, so repeated saves do not pile up as `(1)`, `(2)`. */
export function withTimestamp(fileName: string, at = new Date()) {
  const pad = (value: number) => String(value).padStart(2, "0");
  const stamp =
    `${at.getFullYear()}-${pad(at.getMonth() + 1)}-${pad(at.getDate())}` +
    `_${pad(at.getHours())}-${pad(at.getMinutes())}-${pad(at.getSeconds())}`;
  const dot = fileName.lastIndexOf(".");
  return dot <= 0
    ? `${fileName}-${stamp}`
    : `${fileName.slice(0, dot)}-${stamp}${fileName.slice(dot)}`;
}

const LOADER_PAGE = `<!doctype html>
<html lang="ru"><head><meta charset="utf-8"><title>Подготовка файла…</title><style>
  body{margin:0;height:100vh;display:flex;flex-direction:column;align-items:center;justify-content:center;
    gap:16px;font:16px system-ui,sans-serif;color:#1c1c1c;background:#fff}
  .s{width:40px;height:40px;border:4px solid #d0d0d0;border-top-color:#1976d2;border-radius:50%;
    animation:r 1s linear infinite}
  @keyframes r{to{transform:rotate(360deg)}}
  @media(prefers-color-scheme:dark){body{color:#eee;background:#121212}.s{border-color:#444;border-top-color:#90caf9}}
</style></head><body><div class="s"></div><div>Готовим файл…</div></body></html>`;

export interface ReservedTab {
  /** Puts the file into the reserved tab, or saves it if there is none. */
  show(blob: Blob, fileName: string): void;
  /** Closes the reserved tab when the file never arrived. */
  close(): void;
}

/**
 * Reserves a tab for a file that still has to be fetched. It must be called synchronously from the
 * click: after an await the browser sees no user gesture and blocks the popup. Native gets no tab —
 * the WebView renders no PDF — and neither does a blocked popup, both fall back to `saveBlob`.
 */
export function reserveBlobTab(): ReservedTab {
  // `noopener` would make window.open return null, and the handle is what the tab is reserved for
  const tab = Capacitor.isNativePlatform() ? null : window.open("", "_blank");
  if (tab) {
    tab.opener = null;
    // about:blank inherits this origin, so the blank tab can be given a spinner while the file is fetched
    tab.document.write(LOADER_PAGE);
    tab.document.close();
  }

  return {
    show(blob, fileName) {
      if (!tab || tab.closed) {
        saveBlob(blob, fileName);
        return;
      }
      // never revoked: the tab reads the URL for as long as it stays open, and closing it frees the blob
      tab.location.replace(URL.createObjectURL(blob));
    },
    close() {
      tab?.close();
    },
  };
}
