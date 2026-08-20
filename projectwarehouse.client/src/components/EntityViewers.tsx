import {AvatarGroup, Tooltip} from "@mui/material";
import type {AppEntityType} from "@/api/types.gen";
import {useEntityPresence} from "@/hooks/useEntityPresence";
import UserAvatar from "@/components/UserAvatar";

interface EntityViewersProps {
  entityType: AppEntityType;
  entityId: string | null | undefined;
}

/**
 * "Этот объект просматривают …" in the page header. Unlike the lock banner this says nothing about
 * editing — someone reading the page is not a conflict, just company.
 */
function EntityViewers({entityType, entityId}: EntityViewersProps) {
  const viewers = useEntityPresence(entityType, entityId);
  if (viewers.length === 0) return null;

  return (
    <AvatarGroup
      max={4}
      renderSurplus={(surplus) => (
        <Tooltip
          title={viewers
            .slice(3)
            .map((v) => v.userName)
            .join(", ")}
        >
          <span>+{surplus}</span>
        </Tooltip>
      )}
      sx={{"& .MuiAvatar-root": avatarSx}}
    >
      {viewers.map((viewer) => (
        <Tooltip key={viewer.userId} title={`${viewer.userName} просматривает этот объект`}>
          <UserAvatar userId={viewer.userId} name={viewer.userName} />
        </Tooltip>
      ))}
    </AvatarGroup>
  );
}

const avatarSx = {
  width: 28,
  height: 28,
  fontSize: 13,
  animation: "entityViewerPop 180ms ease-out",
  "@keyframes entityViewerPop": {
    from: {opacity: 0, transform: "scale(0.6)"},
    to: {opacity: 1, transform: "scale(1)"},
  },
};

export default EntityViewers;
