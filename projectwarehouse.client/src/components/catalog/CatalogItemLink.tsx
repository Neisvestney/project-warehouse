import type {ReactNode} from "react";
import type {SxProps, Theme} from "@mui/material/styles";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import HoverActionLink from "@/components/HoverActionLink";

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
    <HoverActionLink
      icon={OpenInNewIcon}
      onClick={() => onOpen(catalogItemId)}
      spacing={spacing}
      sx={sx}
    >
      {children}
    </HoverActionLink>
  );
}

export default CatalogItemLink;
