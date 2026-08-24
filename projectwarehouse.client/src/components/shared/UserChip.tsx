import type {MouseEvent} from "react";
import {Chip, type ChipProps} from "@mui/material";
import {Link} from "react-router";
import {useHasPermission} from "@/hooks/usePermission";

interface UserChipProps extends Omit<ChipProps, "label"> {
  userId: string | null | undefined;
  name: string;
}

function UserChip({userId, name, ...chipProps}: UserChipProps) {
  const canView = useHasPermission("users.view");

  if (!canView || !userId) {
    return <Chip variant="outlined" size="small" label={name} {...chipProps} />;
  }

  return (
    <Chip
      component={Link}
      to={`/settings/employees/${userId}`}
      variant="outlined"
      size="small"
      label={name}
      clickable
      onClick={(e: MouseEvent<HTMLElement>) => e.stopPropagation()}
      {...chipProps}
    />
  );
}

export default UserChip;
