import {TableCell, TableRow, Typography} from "@mui/material";

interface TableRowEmptyProps {
  colSpan: number;
  message: string;
}

function TableRowEmpty({colSpan, message}: TableRowEmptyProps) {
  return (
    <TableRow>
      <TableCell colSpan={colSpan} align="center" sx={{py: 4}}>
        <Typography color="text.secondary">{message}</Typography>
      </TableCell>
    </TableRow>
  );
}

export default TableRowEmpty;
