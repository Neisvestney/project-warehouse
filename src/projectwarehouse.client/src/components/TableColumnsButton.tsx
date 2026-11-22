import {useId, useState} from "react";
import {Checkbox, IconButton, ListItemText, Menu, MenuItem, Tooltip} from "@mui/material";
import ViewColumnIcon from "@mui/icons-material/ViewColumn";
import type {TableColumnOption} from "@/hooks/useTableColumns";

interface TableColumnsButtonProps {
  columns: readonly TableColumnOption[];
  value: readonly string[];
  onChange: (value: string[]) => void;
}

export default function TableColumnsButton({columns, value, onChange}: TableColumnsButtonProps) {
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const id = useId();
  const buttonId = `${id}-button`;
  const menuId = `${id}-menu`;

  if (columns.length === 0) return null;

  return (
    <>
      <Tooltip title="Настроить столбцы">
        <IconButton
          id={buttonId}
          size="small"
          color="inherit"
          aria-label="Настроить столбцы"
          onClick={(event) => setAnchor(event.currentTarget)}
          aria-haspopup="menu"
          aria-controls={anchor ? menuId : undefined}
          aria-expanded={anchor ? true : undefined}
        >
          <ViewColumnIcon fontSize="small" />
        </IconButton>
      </Tooltip>
      <Menu
        id={menuId}
        anchorEl={anchor}
        open={!!anchor}
        onClose={() => setAnchor(null)}
        slotProps={{list: {"aria-labelledby": buttonId}}}
      >
        {columns.map((column) => {
          const checked = value.includes(column.key);
          return (
            <MenuItem
              key={column.key}
              role="menuitemcheckbox"
              aria-checked={checked}
              onClick={() =>
                onChange(
                  checked ? value.filter((key) => key !== column.key) : [...value, column.key],
                )
              }
            >
              <Checkbox
                checked={checked}
                tabIndex={-1}
                disableRipple
                slotProps={{input: {"aria-hidden": true}}}
              />
              <ListItemText primary={column.label} />
            </MenuItem>
          );
        })}
      </Menu>
    </>
  );
}
