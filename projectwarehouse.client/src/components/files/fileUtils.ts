/** Must match DataFilesOptions.ThumbnailWidths — the endpoint rejects anything else. */
export const THUMBNAIL_WIDTHS = [64, 128, 256, 512, 1024] as const;

const IMAGE_EXTENSIONS = ["jpg", "jpeg", "png", "webp", "avif", "gif", "bmp"];

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} Б`;
  const units = ["КБ", "МБ", "ГБ", "ТБ"];
  let value = bytes / 1024;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit += 1;
  }
  return `${value.toFixed(value >= 100 || unit === 0 ? 0 : 1).replace(".", ",")} ${units[unit]}`;
}

/** Rounds up to an allowed width — a raw measurement would be rejected with dataFileWidthNotAllowed. */
export function nearestThumbnailWidth(px: number): number {
  for (const width of THUMBNAIL_WIDTHS) {
    if (width >= px) return width;
  }
  return THUMBNAIL_WIDTHS[THUMBNAIL_WIDTHS.length - 1];
}

export function isImageContentType(contentType: string | undefined | null): boolean {
  return !!contentType && contentType.indexOf("image/") === 0;
}

export function isPdfContentType(contentType: string | undefined | null): boolean {
  return contentType === "application/pdf";
}

/** Guesses a content type from a URL, ignoring the query string. */
export function contentTypeFromUrl(url: string): string | undefined {
  const path = url.split(/[?#]/)[0];
  const dot = path.lastIndexOf(".");
  if (dot < 0) return undefined;

  const ext = path.slice(dot + 1).toLowerCase();
  if (ext === "pdf") return "application/pdf";
  if (ext === "jpg" || ext === "jpeg") return "image/jpeg";
  if (IMAGE_EXTENSIONS.indexOf(ext) >= 0) return `image/${ext}`;
  return undefined;
}

export function fileNameFromUrl(url: string): string {
  const path = url.split(/[?#]/)[0];
  const slash = path.lastIndexOf("/");
  return decodeURIComponent(slash >= 0 ? path.slice(slash + 1) : path) || url;
}

/**
 * A page served over https cannot display an http image — the browser blocks it silently.
 * Rejecting such URLs turns a mystery blank box into an explicit "unsupported" message.
 */
export function isBlockedMixedContent(url: string): boolean {
  return (
    typeof window !== "undefined" &&
    window.location.protocol === "https:" &&
    url.slice(0, 5).toLowerCase() === "http:"
  );
}

export type FileIconKind = "image" | "pdf" | "spreadsheet" | "document" | "text" | "generic";

export function iconKindForContentType(contentType: string | undefined | null): FileIconKind {
  const type = contentType ?? "";
  if (isImageContentType(type)) return "image";
  if (isPdfContentType(type)) return "pdf";
  if (type === "text/csv" || type.indexOf("spreadsheetml") >= 0) return "spreadsheet";
  if (type.indexOf("wordprocessingml") >= 0) return "document";
  if (type.indexOf("text/") === 0) return "text";
  return "generic";
}
