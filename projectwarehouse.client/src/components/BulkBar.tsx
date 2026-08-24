import type {ReactNode} from "react";
import {IconButton, Toolbar, Tooltip, Typography} from "@mui/material";
import ClearIcon from "@mui/icons-material/Clear";
import {pluralCount, type PluralForms} from "@/utils/pluralUtils";

interface BulkBarProps {
  count: number;
  /** Forms of the "N выбран/выбрано" phrase, e.g. `{one: "заказ выбран", ...}`. */
  countLabel: PluralForms;
  onClear: () => void;
  /** Bulk action buttons, aligned to the right edge. */
  children?: ReactNode;
}

/** Toolbar shown above a table while rows are selected. */
function BulkBar({count, countLabel, onClear, children}: BulkBarProps) {
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
      {children}
    </Toolbar>
  );
}

export default BulkBar;
