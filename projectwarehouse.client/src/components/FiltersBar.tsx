import React, {useId, useState} from "react";
import {
  Box,
  ButtonBase,
  Collapse,
  Stack,
  type StackProps,
  Typography,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import FilterAltIcon from "@mui/icons-material/FilterAlt";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";

type FiltersBarProps = Omit<StackProps, "direction" | "spacing"> & {
  children: React.ReactNode;
  actions?: React.ReactNode;
  /** Filters that differ from their defaults; shown only in the collapsed mobile bar, where the controls are hidden. */
  activeCount?: number;
};

function FiltersLabel({activeCount}: {activeCount?: number}) {
  return (
    // minHeight keeps the label centered against size="small" controls even with alignItems: flex-start
    <Stack direction="row" spacing={0.75} sx={{alignItems: "center", flexShrink: 0, minHeight: 40}}>
      <Box
        sx={(theme) => ({
          display: "flex",
          p: 0.5,
          borderRadius: 1,
          color: "primary.main",
          backgroundColor: `color-mix(in srgb, ${theme.vars!.palette.primary.main}, transparent 90%)`,
        })}
      >
        <FilterAltIcon fontSize="small" />
      </Box>
      <Typography color="textSecondary" variant="subtitle2">
        Фильтры
      </Typography>
      {!!activeCount && (
        <Box
          component="span"
          sx={{
            px: 0.75,
            borderRadius: 1,
            typography: "caption",
            fontWeight: 600,
            lineHeight: "20px",
            color: "primary.contrastText",
            bgcolor: "primary.main",
          }}
        >
          {activeCount}
        </Box>
      )}
    </Stack>
  );
}

function FiltersBar({children, actions, activeCount, sx, ...stackProps}: FiltersBarProps) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  const [expanded, setExpanded] = useState(false);
  const controlsId = useId();

  const cardSx = [
    {px: 2, py: 1.5, borderRadius: 2, border: 1, borderColor: "divider"},
    ...(Array.isArray(sx) ? sx : [sx]),
  ];

  if (isMobile) {
    return (
      // stretch goes last: a page's alignItems is meant for the desktop row and would shrink this column
      <Stack sx={[...cardSx, {alignItems: "stretch"}]} {...stackProps}>
        <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
          <ButtonBase
            onClick={() => setExpanded((v) => !v)}
            aria-expanded={expanded}
            aria-controls={controlsId}
            sx={{flexGrow: 1, justifyContent: "space-between", borderRadius: 1, mx: -0.5, px: 0.5}}
          >
            <FiltersLabel activeCount={activeCount} />
            <ExpandMoreIcon
              sx={{
                color: "text.secondary",
                transition: "transform 0.2s",
                transform: expanded ? "rotate(180deg)" : "none",
              }}
            />
          </ButtonBase>
          {actions}
        </Stack>
        <Collapse in={expanded} id={controlsId}>
          <Stack
            spacing={1.5}
            sx={{
              pt: 1.5,
              // doubled so it outranks the widths each page sets on its controls for the desktop row
              "&& > *": {width: "100%", minWidth: 0, maxWidth: "none", flexBasis: "auto"},
            }}
          >
            {children}
          </Stack>
        </Collapse>
      </Stack>
    );
  }

  return (
    <Stack
      spacing={1.5}
      direction="row"
      useFlexGap
      sx={[{alignItems: "center", flexWrap: "wrap"}, ...cardSx]}
      {...stackProps}
    >
      <FiltersLabel />

      {children}

      {actions ? (
        <Stack
          direction="row"
          spacing={1}
          useFlexGap
          sx={{alignItems: "center", flexWrap: "wrap", ml: "auto"}}
        >
          {actions}
        </Stack>
      ) : null}
    </Stack>
  );
}

export default FiltersBar;
