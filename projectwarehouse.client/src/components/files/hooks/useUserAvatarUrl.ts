import {usersGetAvatar} from "@/api";
import {useBlobUrl} from "./useFileBlobUrl";

/** Query key prefix — invalidate it after a user's avatar changes. */
export const userAvatarQueryKey = "user-avatar";

/**
 * Loads a user's avatar by user id, without knowing which file backs it. Users without an avatar
 * answer 404, which surfaces as an error and lets the caller fall back to initials.
 */
export function useUserAvatarUrl(userId: string | null | undefined, width?: number) {
  return useBlobUrl(
    [userAvatarQueryKey, userId, width ?? "original"],
    async (signal) => {
      const response = await usersGetAvatar({
        path: {id: userId!},
        query: width ? {width} : undefined,
        parseAs: "blob",
        signal,
        throwOnError: true,
      });
      return response.data as unknown as Blob;
    },
    !!userId,
  );
}
