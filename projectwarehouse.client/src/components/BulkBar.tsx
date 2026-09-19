import {useState, type ReactNode} from "react";
import {
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
  onClick: () => void;
  /** Replaces the icon with a spinner. */
  pending?: boolean;
  disabled?: boolean;
  /** Shown as a toolbar button; the rest go under «Ещё». Ignored on mobile, where everything is in the menu. */
  primary?: boolean;
  /** Red styling in the menu. */
  danger?: boolean;
}

interface BulkBarProps {
  count: number;
  /** Forms of the "N выбран/выбрано" phrase, e.g. `{one: "заказ выбран", ...}`. */
  countLabel: PluralForms;
  onClear: () => void;
  actions: BulkAction[];
  /** Shown instead of the selection toolbar while no action applies, at the same height. */
  info?: TableInfoStat[];
  infoLoading?: boolean;
}

const buttonSx = {color: "primary.main"};

function actionText(action: BulkAction) {
  return action.count != null ? `${action.label} (${action.count})` : action.label;
}

/**
 * Toolbar shown above a table while rows are selected. With nothing selected it falls back to the
 * list summary in `info`, or renders nothing.
 */
function BulkBar({count, countLabel, onClear, actions, info, infoLoading}: BulkBarProps) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  const [menuAnchor, setMenuAnchor] = useState<HTMLElement | null>(null);

  if (count === 0) return info ? <TableInfoBar stats={info} loading={infoLoading} /> : null;

  const buttonActions = isMobile ? [] : actions.filter((a) => a.primary);
  const menuActions = isMobile ? actions : actions.filter((a) => !a.primary);

  function handleMenuClick(action: BulkAction) {
    setMenuAnchor(null);
    action.onClick();
  }

  return (
    <Toolbar
      variant="dense"
      sx={{
        bgcolor: "primary.main",
        color: "primary.contrastText",
        borderRadius: 1,
        gap: 1,
      }}
    >
      <Typography variant="body2">{pluralCount(count, countLabel)}</Typography>
      <Tooltip title="Очистить выбранное">
        <IconButton size="small" color="inherit" onClick={onClear} sx={{mr: "auto"}}>
          <ClearIcon fontSize="small" />
        </IconButton>
      </Tooltip>
      {buttonActions.map((action) => (
        <Button
          key={action.key}
          size="small"
          variant="contained"
          color="inherit"
          startIcon={action.pending ? <CircularProgress size={14} color="inherit" /> : action.icon}
          disabled={action.disabled}
          onClick={action.onClick}
          sx={buttonSx}
        >
          {actionText(action)}
        </Button>
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
            sx={buttonSx}
          >
            {isMobile ? "Действия" : "Ещё"}
          </Button>
          <Menu anchorEl={menuAnchor} open={menuAnchor != null} onClose={() => setMenuAnchor(null)}>
            {menuActions.map((action) => (
              <MenuItem
                key={action.key}
                disabled={action.disabled}
                onClick={() => handleMenuClick(action)}
                sx={
                  action.danger
                    ? {color: "error.main", "& .MuiListItemIcon-root": {color: "inherit"}}
                    : undefined
                }
              >
                {(action.icon || action.pending) && (
                  <ListItemIcon>
                    {action.pending ? <CircularProgress size={18} color="inherit" /> : action.icon}
                  </ListItemIcon>
                )}
                <ListItemText>{actionText(action)}</ListItemText>
              </MenuItem>
            ))}
          </Menu>
        </>
      )}
    </Toolbar>
  );
}

export default BulkBar;
