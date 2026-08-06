import {useEffect, useMemo, useRef, useState} from "react";
import type {ImgHTMLAttributes, ReactNode} from "react";
import Box from "@mui/material/Box";
import Skeleton from "@mui/material/Skeleton";
import type {DataFileDto} from "@/api";
import {useFileBlobUrl} from "./hooks/useFileBlobUrl";
import {isBlockedMixedContent, nearestThumbnailWidth} from "./fileUtils";
import type {ViewableFile} from "./viewer/viewableFile";

export interface FileImageProps extends Omit<ImgHTMLAttributes<HTMLImageElement>, "src" | "width"> {
  /** Everything the viewer understands, plus the shorthands: a DTO or a bare URL. */
  source: ViewableFile | DataFileDto | string | null | undefined;
  /** Preview width. "auto" measures the container and asks for the nearest allowed size. */
  previewWidth?: number | "auto";
  /** Load only once in the viewport. On by default — a catalog page would otherwise fire a request per row. */
  lazy?: boolean;
  /** Shown when there is no source or it failed to load. */
  fallback?: ReactNode;
}

interface Resolved {
  fileId?: string;
  externalUrl?: string;
  imageWidth?: number | null;
  imageHeight?: number | null;
  alt: string;
}

function resolve(source: FileImageProps["source"]): Resolved | null {
  if (!source) return null;

  if (typeof source === "string") {
    return isBlockedMixedContent(source) ? null : {externalUrl: source, alt: ""};
  }

  if ("kind" in source) {
    if (source.kind === "external") {
      return isBlockedMixedContent(source.url)
        ? null
        : {externalUrl: source.url, alt: source.name ?? ""};
    }
    return {
      fileId: source.file.id,
      imageWidth: source.file.imageWidth,
      imageHeight: source.file.imageHeight,
      alt: source.file.originalFileName,
    };
  }

  return {
    fileId: source.id,
    imageWidth: source.imageWidth,
    imageHeight: source.imageHeight,
    alt: source.originalFileName,
  };
}

export default function FileImage({
  source,
  previewWidth,
  lazy = true,
  fallback = null,
  style,
  ...imgProps
}: FileImageProps) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const resolved = useMemo(() => resolve(source), [source]);

  // IntersectionObserver is missing from the oldest WebViews we target; degrade to eager loading
  // rather than to a permanently blank box
  const [visible, setVisible] = useState(
    () => !lazy || typeof IntersectionObserver === "undefined",
  );
  const [measuredWidth, setMeasuredWidth] = useState<number>();

  // remembering *which* source failed resets the flag on a source change without an effect
  const sourceKey = resolved?.fileId ?? resolved?.externalUrl ?? "";
  const [failedKey, setFailedKey] = useState<string>();
  const failed = !!sourceKey && failedKey === sourceKey;

  useEffect(() => {
    if (visible || !containerRef.current) return;

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((e) => e.isIntersecting)) setVisible(true);
      },
      {rootMargin: "200px"},
    );
    observer.observe(containerRef.current);
    return () => observer.disconnect();
  }, [visible]);

  useEffect(() => {
    if (previewWidth !== "auto" || !containerRef.current) return;

    const element = containerRef.current;
    const update = () => setMeasuredWidth(element.clientWidth || undefined);
    update();

    // ResizeObserver is polyfilled globally in main.tsx
    const observer = new ResizeObserver(update);
    observer.observe(element);
    return () => observer.disconnect();
  }, [previewWidth]);

  const requestedWidth = useMemo(() => {
    if (previewWidth === undefined) return undefined;
    if (typeof previewWidth === "number") return nearestThumbnailWidth(previewWidth);
    if (!measuredWidth) return undefined;
    const dpr = typeof window !== "undefined" ? window.devicePixelRatio || 1 : 1;
    return nearestThumbnailWidth(measuredWidth * dpr);
  }, [previewWidth, measuredWidth]);

  const needsBlob = !!resolved?.fileId && visible;
  const {url: blobUrl, isLoading} = useFileBlobUrl(
    needsBlob ? resolved!.fileId : undefined,
    requestedWidth,
  );

  const src = resolved?.externalUrl ?? blobUrl;

  // aspect-ratio is unsupported in the oldest targeted browsers, so space is reserved with the
  // padding-top percentage trick instead
  const aspectPadding =
    resolved?.imageWidth && resolved.imageHeight
      ? `${(resolved.imageHeight / resolved.imageWidth) * 100}%`
      : undefined;

  const content = () => {
    if (!resolved || failed) return fallback;
    if (!src) {
      return isLoading || needsBlob ? (
        <Skeleton variant="rectangular" width="100%" height="100%" />
      ) : (
        fallback
      );
    }

    return (
      <Box
        component="img"
        src={src}
        alt={resolved.alt}
        loading={lazy ? "lazy" : undefined}
        // keep internal application URLs from leaking to a third-party host
        referrerPolicy={resolved.externalUrl ? "no-referrer" : undefined}
        onError={() => setFailedKey(sourceKey)}
        sx={{width: "100%", height: "100%", objectFit: "cover", display: "block"}}
        {...imgProps}
      />
    );
  };

  return (
    <Box
      ref={containerRef}
      sx={{position: "relative", overflow: "hidden", width: "100%"}}
      style={aspectPadding && !style?.height ? {...style, paddingTop: aspectPadding} : style}
    >
      {aspectPadding && !style?.height ? (
        <Box sx={{position: "absolute", inset: 0}}>{content()}</Box>
      ) : (
        content()
      )}
    </Box>
  );
}
