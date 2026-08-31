import {Box, Typography} from "@mui/material";
import type {Theme} from "@mui/material/styles";
import type {TypographyProps} from "@mui/material/Typography";
import type {SvgIconProps} from "@mui/material/SvgIcon";
import {Link} from "react-router";
import {LogoIcon} from "@/components/icons/LogoIcon";

// The light-scheme app bar is already primary, so the brand mark only takes the accent color in dark.
const brandDarkSx = (theme: Theme) =>
  theme.applyStyles("dark", {color: theme.palette.primary.main});

export interface MainNavBrandProps {
  typographyVariant?: TypographyProps["variant"];
  iconFontSize?: SvgIconProps["fontSize"] | number;
  monospace?: boolean;
  /** "brand": inherits the app bar's text color, accenting only in dark mode. "primary": fixed theme primary color, for use outside the app bar. */
  color?: "brand" | "primary";
  linked?: boolean;
  onClick?: () => void;
  replace?: boolean;
}

function MainNavBrand({
  typographyVariant = "h6",
  iconFontSize,
  monospace = true,
  color = "brand",
  linked = true,
  onClick,
  replace,
}: MainNavBrandProps) {
  const iconSizeProps =
    typeof iconFontSize === "number" ? {sx: {fontSize: iconFontSize}} : {fontSize: iconFontSize};

  const content = (
    <>
      <LogoIcon
        {...iconSizeProps}
        sx={[
          {mr: 1, ...(iconSizeProps.sx ?? {})},
          color === "primary" ? {color: "primary.main"} : brandDarkSx,
          {mb: "2px"},
        ]}
      />
      <Typography
        variant={typographyVariant}
        noWrap
        sx={[
          {
            ...(monospace ? {fontFamily: "monospace", fontWeight: 700} : {}),
            ...(color === "brand" ? {color: "inherit"} : {}),
          },
          color === "brand" ? brandDarkSx : {},
        ]}
      >
        Warehouse
      </Typography>
    </>
  );

  if (!linked) {
    return <Box sx={{display: "flex", alignItems: "center", minWidth: 0}}>{content}</Box>;
  }

  return (
    <Link
      to="/"
      replace={replace}
      onClick={onClick}
      style={{
        display: "flex",
        alignItems: "center",
        minWidth: 0,
        textDecoration: "none",
        color: "inherit",
      }}
    >
      {content}
    </Link>
  );
}

export default MainNavBrand;
