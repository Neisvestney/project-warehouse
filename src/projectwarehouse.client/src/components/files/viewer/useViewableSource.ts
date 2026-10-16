import {useFileBlobUrl} from "../hooks/useFileBlobUrl";
import {contentTypeFromUrl, fileNameFromUrl} from "../fileUtils";
import type {ViewableFile} from "./viewableFile";

export interface ResolvedViewable {
  key: string;
  name: string;
  contentType?: string;
  /** Ready for <img>/<iframe>: an object URL for our files, the link itself for external ones. */
  src?: string;
  isLoading: boolean;
  error?: unknown;
  isExternal: boolean;
  imageWidth?: number | null;
  imageHeight?: number | null;
  /** Only our own files carry this — an external link has no such data. */
  meta?: {sizeBytes: number; createdAt: string; createdByUserName?: string | null};
  download: {mode: "blob" | "newTab"; fileName?: string; url?: string};
}

/**
 * Collapses both source kinds into one shape so the renderers never branch on `kind`.
 * The query is always called and switched off via `enabled` — a conditional hook is not allowed.
 */
export function useViewableSource(item: ViewableFile): ResolvedViewable {
  const fileId = item.kind === "dataFile" ? item.file.id : undefined;
  const {url, isLoading, error} = useFileBlobUrl(fileId);

  if (item.kind === "external") {
    return {
      key: item.url,
      name: item.name ?? fileNameFromUrl(item.url),
      // no extra request just to learn the type: that is a round trip and runs into CORS
      contentType: item.contentType ?? contentTypeFromUrl(item.url) ?? "image/*",
      src: item.url,
      // the browser loads it, so there is no loading state we could observe
      isLoading: false,
      isExternal: true,
      // a cross-origin `download` attribute is ignored by the browser, so downloading becomes opening
      download: {mode: "newTab", url: item.url},
    };
  }

  const {file} = item;
  return {
    key: file.id,
    name: file.originalFileName,
    contentType: file.contentType,
    src: url,
    isLoading,
    error,
    isExternal: false,
    imageWidth: file.imageWidth,
    imageHeight: file.imageHeight,
    meta: {
      sizeBytes: file.sizeBytes,
      createdAt: file.createdAt,
      createdByUserName: file.createdByUserName,
    },
    download: {mode: "blob", fileName: file.originalFileName, url},
  };
}
