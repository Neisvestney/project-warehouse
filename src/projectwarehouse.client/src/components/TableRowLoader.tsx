import {CircularProgress, TableCell, TableRow} from "@mui/material";

interface TableRowLoaderProps {
  colSpan: number;
}

function TableRowLoader({colSpan}: TableRowLoaderProps) {
  return (
    <TableRow>
      <TableCell colSpan={colSpan} align="center" sx={{py: 4}}>
        <CircularProgress size={32} />
      </TableCell>
    </TableRow>
  );
}

export default TableRowLoader;
