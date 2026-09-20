import {ButtonBase, Tooltip} from "@mui/material";
import type {DataFileDto} from "@/api";
import UserAvatar from "@/components/UserAvatar";
import FileViewerModal from "@/components/files/viewer/FileViewerModal";
import {viewable} from "@/components/files/viewer/viewableFile";
import {useModal} from "@/hooks/useModal";

interface ViewableUserAvatarProps {
  userId: string | null | undefined;
  name?: string | null;
  /** The file backing the avatar. Without it there is nothing to open, so the avatar stays inert. */
  avatar?: DataFileDto | null;
  size?: number;
  previewWidth?: number;
}

function ViewableUserAvatar({
  userId,
  name,
  avatar,
  size = 120,
  previewWidth = 256,
}: ViewableUserAvatarProps) {
  const {showModal} = useModal();

  const avatarEl = (
    <UserAvatar
      userId={userId}
      name={name}
      previewWidth={previewWidth}
      sx={{width: size, height: size, fontSize: size * 0.4, flexShrink: 0}}
    />
  );

  if (!avatar) return avatarEl;

  return (
    <Tooltip title="Открыть фото">
      <ButtonBase
        onClick={() => void showModal(FileViewerModal, {files: [viewable(avatar)]})}
        sx={{borderRadius: "50%", flexShrink: 0}}
      >
        {avatarEl}
      </ButtonBase>
    </Tooltip>
  );
}

export default ViewableUserAvatar;
