/**
 * Hands a generated file to the browser. An anchor with `download` is the only save mechanism the app
 * has, and it is also the right one on native: the Android WebView cannot render a PDF inline, so
 * passing the file to the system viewer is the documented behaviour (docs/native-client.md).
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
