import {useState} from "react";
import {Avatar, type AvatarProps} from "@mui/material";
import {userColor} from "@/utils/userColor";
import {useUserAvatarUrl} from "@/components/files/hooks/useUserAvatarUrl";

interface UserAvatarProps extends Omit<AvatarProps, "children"> {
  userId: string | null | undefined;
  name?: string | null;
  /** Preview width to request. Must be one of the allowed thumbnail widths. */
  previewWidth?: number;
}

/**
 * Avatar with the user's uploaded photo, falling back to a background color derived from the user
 * id, so the same person keeps the same color everywhere (presence, app bar, tables).
 */
function UserAvatar({
  userId,
  name,
  previewWidth = 128,
  sx,
  slotProps,
  ...avatarProps
}: UserAvatarProps) {
  const letter = name?.trim()?.[0]?.toUpperCase() ?? "?";
  const {url} = useUserAvatarUrl(userId, previewWidth);
  // a blob already in the query cache arrives on the first render — show it without the fade
  const [cachedUrl] = useState(url);
  const [loadedUrl, setLoadedUrl] = useState(cachedUrl);

  return (
    <Avatar
      src={url}
      sx={[
        {
          bgcolor: userColor(userId),
          color: "#fff",
          "& .MuiAvatar-img": {
            opacity: url && loadedUrl === url ? 1 : 0,
            transition:
              url && url === cachedUrl
                ? "none"
                : (theme) => theme.transitions.create("opacity", {duration: 300}),
          },
        },
        ...(Array.isArray(sx) ? sx : [sx]),
      ]}
      slotProps={{
        ...slotProps,
        img: {
          ...slotProps?.img,
          onLoad: () => setLoadedUrl(url),
        },
      }}
      {...avatarProps}
    >
      {letter}
    </Avatar>
  );
}

export default UserAvatar;
