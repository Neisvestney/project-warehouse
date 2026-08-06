import {useEffect, useMemo} from "react";
import {useQuery} from "@tanstack/react-query";
import {filesGetContent, filesGetThumbnail} from "@/api";

/**
 * Revocations are deferred by a tick so that a cleanup immediately followed by a setup — StrictMode
 * on mount, and any future offscreen remount — cancels itself instead of killing a live URL.
 */
const pendingRevokes = new Map<string, ReturnType<typeof setTimeout>>();

/**
 * Loads file content as an object URL.
 *
 * Images cannot use a plain `<img src="/api/files/...">`: the bearer token is injected by the
 * request interceptor in `services/apiClient.ts`, and an `src` attribute carries no Authorization
 * header. The cost is no browser HTTP cache — React Query's cache replaces it, keyed by id + width,
 * so the same image in a list and in the viewer is fetched once.
 */
export function useFileBlobUrl(fileId: string | undefined, width?: number) {
  const {
    data: blob,
    isPending,
    error,
  } = useQuery({
    queryKey: ["file-blob", fileId, width ?? "original"],
    // hand-written rather than the generated *Options: the generator's response type for binary
    // endpoints is unreliable, and the width branch cannot be two conditional hooks
    queryFn: async ({signal}) => {
      const response = width
        ? await filesGetThumbnail({
            path: {id: fileId!},
            query: {width},
            parseAs: "blob",
            signal,
            throwOnError: true,
          })
        : await filesGetContent({path: {id: fileId!}, parseAs: "blob", signal, throwOnError: true});
      return response.data as unknown as Blob;
    },
    enabled: !!fileId,
    staleTime: Infinity,
    // bounds memory: a long catalog session would otherwise pin hundreds of blobs
    gcTime: 30 * 60_000,
  });

  // createObjectURL returns a distinct URL per call, so consumers never revoke each other's
  const url = useMemo(() => (blob ? URL.createObjectURL(blob) : undefined), [blob]);

  useEffect(() => {
    if (!url) return;

    const pending = pendingRevokes.get(url);
    if (pending !== undefined) {
      clearTimeout(pending);
      pendingRevokes.delete(url);
    }

    return () => {
      pendingRevokes.set(
        url,
        setTimeout(() => {
          pendingRevokes.delete(url);
          URL.revokeObjectURL(url);
        }, 0),
      );
    };
  }, [url]);

  return {url, isLoading: !!fileId && isPending, error};
}
