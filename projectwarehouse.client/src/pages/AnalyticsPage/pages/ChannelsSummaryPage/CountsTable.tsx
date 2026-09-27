import {Table, TableBody, TableCell, TableHead, TableRow} from "@mui/material";
import {formatNumber} from "./channelsSummaryUtils";

interface CountsTableProps {
  label: string;
  valueLabel: string;
  rows: {key: string; count: number}[];
}

/** A short «name — count» table for the top reasons under a card's chart. */
function CountsTable({label, valueLabel, rows}: CountsTableProps) {
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
            <TableCell align="right">{formatNumber(row.count)}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

export default CountsTable;
