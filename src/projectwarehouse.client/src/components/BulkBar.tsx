import {useLayoutEffect, useRef, useState, type MouseEvent, type ReactNode} from "react";
import {
  Box,
  Button,
  CircularProgress,
  IconButton,
  ListItemIcon,
  ListItemText,
  Menu,
  MenuItem,
  Toolbar,
  Tooltip,
  Typography,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import ClearIcon from "@mui/icons-material/Clear";
import ArrowDropDownIcon from "@mui/icons-material/ArrowDropDown";
import {pluralCount, type PluralForms} from "@/utils/pluralUtils";
import TableInfoBar, {type TableInfoStat} from "@/components/TableInfoBar";

export interface BulkAction {
  key: string;
  label: string;
  icon?: ReactNode;
  /** Appended to the label as "(N)"; omit when the action applies to the whole selection. */
  count?: number;
  onClick: (event: MouseEvent<HTMLElement>) => void;
  /** Replaces the icon with a spinner. */
  pending?: boolean;
  disabled?: boolean;
  /** Always a desktop button; other actions become buttons when space permits. */
  primary?: boolean;
  /** Red styling in the menu and on toolbar buttons. */
  danger?: boolean;
}

interface BulkBarProps {
  count: number;
  /** Forms of the "N выбран/выбрано" phrase, e.g. `{one: "заказ выбран", ...}`. */
  countLabel: PluralForms;
  onClear: () => void;
  /** Part of `count` the current filters hide; shown next to it when non-zero. */
  hiddenCount?: number;
  actions: BulkAction[];
  info?: TableInfoStat[];
  infoLoading?: boolean;
  infoActions?: ReactNode;
}

const buttonSx = {color: "primary.main"};
const dangerButtonSx = {color: "error.main"};
const promotionSpace = 48;
const retentionSpace = 16;

function actionText(action: BulkAction) {
  return action.count != null ? `${action.label} (${action.count})` : action.label;
}

function BulkActionButton({action}: {action: BulkAction}) {
  return (
    <Button
      size="small"
      variant="contained"
      color="inherit"
      startIcon={action.pending ? <CircularProgress size={14} color="inherit" /> : action.icon}
      disabled={action.disabled}
      onClick={action.onClick}
      sx={{...(action.danger ? dangerButtonSx : buttonSx), flexShrink: 0, whiteSpace: "nowrap"}}
    >
      {actionText(action)}
    </Button>
  );
}

function BulkBar({
  count,
  countLabel,
  onClear,
  hiddenCount,
  actions,
  info,
  infoLoading,
  infoActions,
}: BulkBarProps) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  const [menuAnchor, setMenuAnchor] = useState<HTMLElement | null>(null);
  const [promotedKeys, setPromotedKeys] = useState<string[]>([]);
  const toolbarRef = useRef<HTMLDivElement>(null);
  const selectionRef = useRef<HTMLDivElement>(null);
  const measurementRef = useRef<HTMLDivElement>(null);
  const infoMeasurementRef = useRef<HTMLDivElement>(null);
  if (count === 0 && menuAnchor !== null) setMenuAnchor(null);

  useLayoutEffect(() => {
    const toolbar = toolbarRef.current;
    const selection = selectionRef.current;
    const measurement = measurementRef.current;
    const infoMeasurement = infoMeasurementRef.current;
    if (!toolbar || !selection || !measurement || isMobile) return;

    const measure = () => {
      const style = getComputedStyle(toolbar);
      const gap = parseFloat(style.columnGap) || 0;
      const available =
        toolbar.clientWidth - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight);
      const widths = Array.from(
        measurement.children,
        (child) => child.getBoundingClientRect().width,
      );
      let baseWidth = Math.max(
        selection.getBoundingClientRect().width,
        infoMeasurement?.getBoundingClientRect().width ?? 0,
      );
      const secondary: {key: string; width: number}[] = [];
      actions.forEach((action, index) => {
        if (action.primary) baseWidth += widths[index] + gap;
        else secondary.push({key: action.key, width: widths[index] + gap});
      });

      setPromotedKeys((previous) => {
        let required = baseWidth;
        let visibleCount = 0;
        for (let index = 0; index < secondary.length; index++) {
          required += secondary[index].width;
          const menuWidth = index < secondary.length - 1 ? widths[actions.length] + gap : 0;
          const reserve = previous.includes(secondary[index].key) ? retentionSpace : promotionSpace;
          if (required + menuWidth + reserve <= available) visibleCount = index + 1;
        }
        const next = secondary.slice(0, visibleCount).map((action) => action.key);
        return next.length === previous.length &&
          next.every((key, index) => key === previous[index])
          ? previous
          : next;
      });
    };

    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(toolbar);
    observer.observe(selection);
    observer.observe(measurement);
    if (infoMeasurement) observer.observe(infoMeasurement);
    return () => observer.disconnect();
  });

  if (count === 0 && !info) return null;

  let buttonActions = isMobile
    ? []
    : actions.filter((a) => a.primary || promotedKeys.includes(a.key));
  let menuActions = isMobile
    ? actions
    : actions.filter((a) => !a.primary && !promotedKeys.includes(a.key));
  // a dropdown holding a single action and nothing beside it is just an extra click
  if (buttonActions.length === 0 && menuActions.length === 1) {
    buttonActions = menuActions;
    menuActions = [];
  }

  function handleMenuClick(action: BulkAction, event: MouseEvent<HTMLElement>) {
    setMenuAnchor(null);
    action.onClick(event);
  }

  return (
    <Box
      sx={{
        display: "grid",
        gridTemplateColumns: "minmax(0, 1fr)",
        position: count > 0 ? "sticky" : "relative",
        top: count > 0 ? 6 : undefined,
        zIndex: theme.zIndex.appBar - 2,
      }}
    >
      {info && (
        <Box
          aria-hidden={count > 0}
          inert={count > 0}
          sx={{gridArea: "1 / 1", display: "grid", visibility: count > 0 ? "hidden" : "visible"}}
        >
          <TableInfoBar stats={info} loading={infoLoading}>
            {infoActions}
          </TableInfoBar>
        </Box>
      )}
      <Toolbar
        ref={toolbarRef}
        aria-hidden={count === 0}
        inert={count === 0}
        variant="dense"
        sx={{
          gridArea: "1 / 1",
          visibility: count === 0 ? "hidden" : "visible",
          bgcolor: "primary.main",
          color: "primary.contrastText",
          borderRadius: 1,
          flexWrap: "wrap",
          alignItems: "center",
          gap: 1,
          py: 0.5,
        }}
      >
        <Box sx={{flex: "1 1 min-content", minWidth: 0, overflow: "hidden"}}>
          <Box
            ref={selectionRef}
            sx={{
              display: "flex",
              alignItems: "center",
              gap: 1,
              minHeight: 32,
              width: isMobile ? undefined : "max-content",
            }}
          >
            <Typography variant="body2" noWrap>
              {pluralCount(count, countLabel)}
            </Typography>
            {!!hiddenCount && (
              <Tooltip title="Выбраны, но скрыты текущими фильтрами — действия их тоже затронут">
                <Typography variant="body2" sx={{opacity: 0.8, whiteSpace: "nowrap"}}>
                  (скрыто: {hiddenCount.toLocaleString("ru-RU")})
                </Typography>
              </Tooltip>
            )}
            <Tooltip title="Очистить выбранное">
              <IconButton size="small" color="inherit" onClick={onClear} sx={{p: 0.5}}>
                <ClearIcon fontSize="small" />
              </IconButton>
            </Tooltip>
          </Box>
          {info && <TableInfoBar stats={info} loading={infoLoading} compact />}
        </Box>
        {buttonActions.map((action) => (
          <BulkActionButton key={action.key} action={action} />
        ))}
        {menuActions.length > 0 && (
          <>
            <Button
              size="small"
              variant="contained"
              color="inherit"
              endIcon={
                menuActions.some((a) => a.pending) ? (
                  <CircularProgress size={14} color="inherit" />
                ) : (
                  <ArrowDropDownIcon />
                )
              }
              disabled={menuActions.every((a) => a.disabled)}
              onClick={(e) => setMenuAnchor(e.currentTarget)}
              sx={{...buttonSx, flexShrink: 0, whiteSpace: "nowrap"}}
            >
              {isMobile ? "Действия" : "Ещё"}
            </Button>
            <Menu
              anchorEl={menuAnchor}
              open={menuAnchor != null}
              onClose={() => setMenuAnchor(null)}
            >
              {menuActions.map((action) => (
                <MenuItem
                  key={action.key}
                  disabled={action.disabled}
                  onClick={(event) => handleMenuClick(action, event)}
                  sx={
                    action.danger
                      ? {color: "error.main", "& .MuiListItemIcon-root": {color: "inherit"}}
                      : undefined
                  }
                >
                  {(action.icon || action.pending) && (
                    <ListItemIcon>
                      {action.pending ? (
                        <CircularProgress size={18} color="inherit" />
                      ) : (
                        action.icon
                      )}
                    </ListItemIcon>
                  )}
                  <ListItemText>{actionText(action)}</ListItemText>
                </MenuItem>
              ))}
            </Menu>
          </>
        )}
        {!isMobile && (
          <Box
            aria-hidden
            inert
            sx={{
              position: "absolute",
              visibility: "hidden",
              width: 0,
              height: 0,
              overflow: "hidden",
              pointerEvents: "none",
            }}
          >
            <Box ref={measurementRef} sx={{display: "flex", width: "max-content"}}>
              {actions.map((action) => (
                <BulkActionButton key={action.key} action={action} />
              ))}
              <Button
                size="small"
                variant="contained"
                color="inherit"
                endIcon={<ArrowDropDownIcon />}
                sx={{flexShrink: 0, whiteSpace: "nowrap"}}
              >
                Ещё
              </Button>
            </Box>
            {info && (
              <Box ref={infoMeasurementRef} sx={{width: "max-content"}}>
                <TableInfoBar stats={info} loading={infoLoading} compact />
              </Box>
            )}
          </Box>
        )}
      </Toolbar>
    </Box>
  );
}

export default BulkBar;
