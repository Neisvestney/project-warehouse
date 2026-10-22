import React, {useId, useState} from "react";
import {
  Box,
  ButtonBase,
  Collapse,
  Stack,
  type StackProps,
  type SxProps,
  type Theme,
  Tooltip,
  Typography,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import FilterAltIcon from "@mui/icons-material/FilterAlt";
import CloseIcon from "@mui/icons-material/Close";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";

type FiltersBarProps = Omit<StackProps, "direction" | "spacing"> & {
  children: React.ReactNode;
  actions?: React.ReactNode;
  /** Filters that differ from their defaults; tints the icon, and the collapsed mobile bar shows it as a count. */
  activeCount?: number;
  /** Resets every control of the bar to its default; the icon turns into a reset button while filters are active. */
  onReset?: () => void;
};

function FiltersIcon({active, onReset}: {active: boolean; onReset?: () => void}) {
  const iconSx: SxProps<Theme> = (theme) => ({
    display: "flex",
    p: 0.5,
    borderRadius: 1,
    color: active ? "primary.main" : "text.disabled",
    backgroundColor: active
      ? `color-mix(in srgb, ${theme.vars!.palette.primary.main}, transparent 90%)`
      : theme.vars!.palette.action.hover,
  });

  if (!onReset) {
    return (
      <Box sx={iconSx}>
        <FilterAltIcon fontSize="small" />
      </Box>
    );
  }

  const showReset = {
    "& .filters-icon": {opacity: 0, transform: "rotate(90deg) scale(0.5)"},
    "& .filters-reset-icon": {opacity: 1, transform: "none"},
  };

  // Stays a button when inactive (aria-disabled, not disabled), so a reset does not drop keyboard focus
  return (
    <Tooltip title={active ? "Сбросить фильтры" : ""}>
      <ButtonBase
        focusRipple
        disableRipple={!active}
        onClick={() => active && onReset()}
        aria-label="Сбросить фильтры"
        aria-disabled={!active}
        tabIndex={active ? 0 : -1}
        sx={[
          iconSx,
          (theme) => ({
            position: "relative",
            cursor: active ? "pointer" : "default",
            transition: theme.transitions.create(["color", "background-color"]),
            // widens the 28px hit area for touch without moving the layout
            "&::before": {content: '""', position: "absolute", inset: -4},
            "& .filters-icon, & .filters-reset-icon": {
              transition: theme.transitions.create(["opacity", "transform"], {
                duration: theme.transitions.duration.short,
              }),
            },
            "& .filters-reset-icon": {
              position: "absolute",
              opacity: 0,
              transform: "rotate(-90deg) scale(0.5)",
            },
            "@media (prefers-reduced-motion: reduce)": {
              "&, & .filters-icon, & .filters-reset-icon": {transition: "none"},
            },
          }),
          active && {
            "&:hover, &.Mui-focusVisible": showReset,
            // touch screens have no hover, so the cross stays visible while filters are on
            "@media (hover: none)": showReset,
          },
        ]}
      >
        <FilterAltIcon fontSize="small" className="filters-icon" />
        <CloseIcon fontSize="small" className="filters-reset-icon" />
      </ButtonBase>
    </Tooltip>
  );
}

function FiltersLabel({activeCount}: {activeCount?: number}) {
  return (
    // minHeight keeps the label centered against size="small" controls even with alignItems: flex-start
    <Stack direction="row" spacing={0.75} sx={{alignItems: "center", flexShrink: 0, minHeight: 40}}>
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

function FiltersBar({children, actions, activeCount, onReset, sx, ...stackProps}: FiltersBarProps) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  const [expanded, setExpanded] = useState(false);
  const controlsId = useId();

  const cardSx = [
    {
      px: 2,
      py: 1.5,
      borderRadius: 2,
      border: 1,
      borderColor: "divider",
      // size="small" buttons are shorter than the 40px size="small" inputs
      "& .MuiButton-root, & .MuiToggleButton-root": {height: 40},
    },
    ...(Array.isArray(sx) ? sx : [sx]),
  ];

  if (isMobile) {
    return (
      // stretch goes last: a page's alignItems is meant for the desktop row and would shrink this column
      <Stack sx={[...cardSx, {alignItems: "stretch"}]} {...stackProps}>
        <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
          {/* the icon stays outside the toggle: a button cannot nest inside another button */}
          {/* gap + the toggle's padding add up to the desktop 6px between icon and caption */}
          <Stack direction="row" spacing={0.25} useFlexGap sx={{alignItems: "center", flexGrow: 1}}>
            <FiltersIcon active={!!activeCount} onReset={onReset} />
            <ButtonBase
              onClick={() => setExpanded((v) => !v)}
              aria-expanded={expanded}
              aria-controls={controlsId}
              sx={{
                flexGrow: 1,
                justifyContent: "space-between",
                borderRadius: 1,
                mr: -0.5,
                px: 0.5,
              }}
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
          </Stack>
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
      <Stack direction="row" spacing={0.75} sx={{alignItems: "center", flexShrink: 0}}>
        <FiltersIcon active={!!activeCount} onReset={onReset} />
        <FiltersLabel />
      </Stack>

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
