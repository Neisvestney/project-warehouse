import {useEffect, useRef, type ReactNode} from "react";
import {Alert, Box, CircularProgress, GlobalStyles, Typography} from "@mui/material";
import {extractErrorMessage} from "@/utils/errorUtils";

const cellSx = {
  border: "0.5pt solid #000",
  px: "2mm",
  height: "9mm",
  fontSize: "10pt",
  verticalAlign: "middle",
} as const;

export interface PrintTableColumn {
  header: string;
  width?: string;
  align?: "left" | "center";
}

export interface PrintTableRow {
  key: string;
  cells: ReactNode[];
}

export interface PrintTableSheet {
  title: ReactNode;
  rows: PrintTableRow[];
}

interface PrintTablePageProps {
  columns: PrintTableColumn[];
  sheet: PrintTableSheet | undefined;
  isLoading: boolean;
  error: unknown;
  errorMessage: string;
  blankRows?: number;
}

function PrintTablePage({
  columns,
  sheet,
  isLoading,
  error,
  errorMessage,
  blankRows = 0,
}: PrintTablePageProps) {
  const printedRef = useRef(false);

  useEffect(() => {
    if (!sheet || printedRef.current) return;
    printedRef.current = true;
    // Let the browser lay out the table before the print dialog snapshots it
    requestAnimationFrame(() => window.print());
  }, [sheet]);

  if (isLoading) {
    return (
      <Box sx={{display: "flex", justifyContent: "center", py: 6}}>
        <CircularProgress />
      </Box>
    );
  }

  if (!sheet) {
    return (
      <Box sx={{p: 3}}>
        <Alert severity="error">{extractErrorMessage(error) || errorMessage}</Alert>
      </Box>
    );
  }

  const rows: PrintTableRow[] = [
    ...sheet.rows,
    ...Array.from({length: blankRows}, (_, i) => ({key: `blank:${i}`, cells: []})),
  ];

  return (
    <Box sx={{bgcolor: "#fff", color: "#000", minHeight: "100vh"}}>
      <GlobalStyles styles={{"@page": {size: "A4 portrait", margin: "10mm"}}} />
      <Box
        sx={{maxWidth: "190mm", mx: "auto", p: "10mm", "@media print": {p: 0, maxWidth: "none"}}}
      >
        <Typography component="div" sx={{fontSize: "12pt", fontWeight: 600, mb: "3mm"}}>
          {sheet.title}
        </Typography>
        <Box component="table" sx={{width: "100%", borderCollapse: "collapse"}}>
          <Box component="thead" sx={{display: "table-header-group"}}>
            <tr>
              <Box component="th" sx={{...cellSx, width: "10mm"}}>
                №
              </Box>
              {columns.map((col) => (
                <Box
                  component="th"
                  key={col.header}
                  sx={{...cellSx, width: col.width, textAlign: col.align ?? "center"}}
                >
                  {col.header}
                </Box>
              ))}
            </tr>
          </Box>
          <tbody>
            {rows.map((row, i) => (
              <Box component="tr" key={row.key} sx={{breakInside: "avoid"}}>
                <Box component="td" sx={{...cellSx, textAlign: "center"}}>
                  {i + 1}
                </Box>
                {columns.map((col, c) => (
                  <Box
                    component="td"
                    key={col.header}
                    sx={{...cellSx, textAlign: col.align ?? "center", overflowWrap: "anywhere"}}
                  >
                    {row.cells[c]}
                  </Box>
                ))}
              </Box>
            ))}
          </tbody>
        </Box>
      </Box>
    </Box>
  );
}

export default PrintTablePage;
