import type {ReactNode} from "react";
import {Stack} from "@mui/material";
import type {SxProps, Theme} from "@mui/material/styles";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";

interface CatalogItemLinkProps {
  catalogItemId: string;
  onOpen: (id: string) => void;
  spacing?: number;
  sx?: SxProps<Theme>;
  children: ReactNode;
}

export function CatalogItemLink({
  catalogItemId,
  onOpen,
  spacing = 1,
  sx,
  children,
}: CatalogItemLinkProps) {
  return (
    <Stack
      direction="row"
      spacing={spacing}
      sx={[
        {
          alignItems: "center",
          cursor: "pointer",
          width: "fit-content",
          "& .open-icon": {visibility: "hidden"},
          "&:hover .open-icon": {visibility: "visible"},
        },
        ...(Array.isArray(sx) ? sx : [sx]),
      ]}
      onClick={(e) => {
        e.stopPropagation();
        onOpen(catalogItemId);
      }}
    >
      {children}
      <OpenInNewIcon
        className="open-icon"
        sx={{fontSize: 14, color: "text.secondary", flexShrink: 0}}
      />
    </Stack>
  );
}

export default CatalogItemLink;
