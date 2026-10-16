import {TableCell, type TableCellProps, Typography} from "@mui/material";

interface DateTimeTableCellProps extends Omit<TableCellProps, "children"> {
  value: string | null | undefined;
}

/** Date on the first line, time under it, with tightened line heights so the row barely grows. */
function DateTimeTableCell({value, sx, ...props}: DateTimeTableCellProps) {
  const date = value ? new Date(value) : null;

  return (
    <TableCell sx={{whiteSpace: "nowrap", py: 0.25, ...sx}} {...props}>
      {date ? (
        <>
          <Typography variant="body2" sx={{lineHeight: 1.25}}>
            {date.toLocaleDateString("ru-RU")}
          </Typography>
          <Typography
            variant="caption"
            color="text.secondary"
            sx={{display: "block", lineHeight: 1.15}}
          >
            {date.toLocaleTimeString("ru-RU", {hour: "2-digit", minute: "2-digit"})}
          </Typography>
        </>
      ) : (
        "—"
      )}
    </TableCell>
  );
}

export default DateTimeTableCell;
