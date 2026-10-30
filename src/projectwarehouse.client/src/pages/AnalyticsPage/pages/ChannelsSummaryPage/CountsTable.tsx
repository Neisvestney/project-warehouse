import {Table, TableBody, TableCell, TableHead, TableRow} from "@mui/material";
import {formatNumber} from "@/components/analytics/analyticsFormat";

interface CountsTableProps {
  label: string;
  valueLabel: string;
  rows: {key: string; count: number}[];
  format?: (value: number) => string;
}

/** A short «name — count» table for the top reasons under a card's chart. */
function CountsTable({label, valueLabel, rows, format = formatNumber}: CountsTableProps) {
  if (rows.length === 0) return null;

  return (
    <Table size="small">
      <TableHead>
        <TableRow>
          <TableCell>{label}</TableCell>
          <TableCell align="right">{valueLabel}</TableCell>
        </TableRow>
      </TableHead>
      <TableBody>
        {rows.map((row) => (
          <TableRow key={row.key}>
            <TableCell sx={{wordBreak: "break-word"}}>{row.key}</TableCell>
            <TableCell align="right">{format(row.count)}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

export default CountsTable;
