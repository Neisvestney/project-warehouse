import {Avatar, type AvatarProps} from "@mui/material";
import {userColor} from "@/utils/userColor";

interface UserAvatarProps extends Omit<AvatarProps, "children"> {
  userId: string | null | undefined;
  name?: string | null;
}

/**
 * Avatar with a background color derived from the user id, so the same person keeps the same color
 * everywhere (presence, app bar, tables).
 */
function UserAvatar({userId, name, sx, ...avatarProps}: UserAvatarProps) {
  const letter = name?.trim()?.[0]?.toUpperCase() ?? "?";

  return (
    <Avatar
      sx={[{bgcolor: userColor(userId), color: "#fff"}, ...(Array.isArray(sx) ? sx : [sx])]}
      {...avatarProps}
    >
      {letter}
    </Avatar>
  );
}

export default UserAvatar;
