import {TableCell, type TableCellProps, Tooltip, Typography} from "@mui/material";

interface NotesTableCellProps extends TableCellProps {
  notes?: string | null;
  maxWidth?: number;
}

function NotesTableCell({notes, maxWidth = 200, sx, ...props}: NotesTableCellProps) {
  return (
    <TableCell sx={sx} {...props}>
      <Tooltip title={notes ?? ""} disableHoverListener={!notes}>
        <Typography
          variant="body2"
          color="text.secondary"
          noWrap
          sx={{maxWidth, display: "inline-block", verticalAlign: "bottom"}}
        >
          {notes || "—"}
        </Typography>
      </Tooltip>
    </TableCell>
  );
}

export default NotesTableCell;
