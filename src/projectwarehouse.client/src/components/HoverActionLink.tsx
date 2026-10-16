import type {KeyboardEvent, MouseEvent, ReactNode} from "react";
import {Stack} from "@mui/material";
import type {SvgIconComponent} from "@mui/icons-material";
import type {SxProps, Theme} from "@mui/material/styles";

interface HoverActionLinkProps {
  icon: SvgIconComponent;
  onClick: (e: MouseEvent<HTMLDivElement> | KeyboardEvent<HTMLDivElement>) => void;
  ariaLabel?: string;
  spacing?: number;
  sx?: SxProps<Theme>;
  children: ReactNode;
}

function HoverActionLink({
  icon: Icon,
  onClick,
  ariaLabel,
  spacing = 1,
  sx,
  children,
}: HoverActionLinkProps) {
  return (
    <Stack
      direction="row"
      spacing={spacing}
      role="button"
      tabIndex={0}
      aria-label={ariaLabel}
      sx={[
        {
          alignItems: "center",
          cursor: "pointer",
          width: "fit-content",
          "& .hover-action-icon": {visibility: "hidden"},
          "&:hover .hover-action-icon, &:focus-visible .hover-action-icon": {visibility: "visible"},
          "@media (hover: none)": {"& .hover-action-icon": {visibility: "visible"}},
        },
        ...(Array.isArray(sx) ? sx : [sx]),
      ]}
      onClick={(e) => {
        e.stopPropagation();
        onClick(e);
      }}
      onKeyDown={(e) => {
        if (e.key !== "Enter" && e.key !== " ") return;
        // Space would scroll the page, and both keys would toggle an enclosing accordion
        e.preventDefault();
        e.stopPropagation();
        onClick(e);
      }}
    >
      {children}
      <Icon
        className="hover-action-icon"
        sx={{fontSize: 14, color: "text.secondary", flexShrink: 0}}
      />
    </Stack>
  );
}

export default HoverActionLink;
