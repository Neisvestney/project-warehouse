import {useEffect, useRef} from "react";
import {useParams} from "react-router";
import {Alert, Box, CircularProgress, GlobalStyles, Typography} from "@mui/material";
import {useQuery} from "@tanstack/react-query";
import {stocktakesGetNodeStockOptions} from "@/api/@tanstack/react-query.gen";
import {formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";
import {compareDraftRows} from "@/components/stocktakes/stocktakeDraft";
import {extractErrorMessage} from "@/utils/errorUtils";

const BLANK_ROWS = 5;

const cellSx = {
  border: "0.5pt solid #000",
  px: "2mm",
  height: "9mm",
  fontSize: "10pt",
  verticalAlign: "middle",
} as const;

interface SheetRow {
  key: string;
  name: string;
  expected: number | null;
}

function StocktakeNodePrintPage() {
  const {id = "", nodeId = ""} = useParams();
  const printedRef = useRef(false);

  const stockQuery = useQuery(stocktakesGetNodeStockOptions({path: {id, nodeId}}));
  const stock = stockQuery.data;

  useEffect(() => {
    if (!stock || printedRef.current) return;
    printedRef.current = true;
    // Let the browser lay out the table before the print dialog snapshots it
    requestAnimationFrame(() => window.print());
  }, [stock]);

  if (stockQuery.isLoading) {
    return (
      <Box sx={{display: "flex", justifyContent: "center", py: 6}}>
        <CircularProgress />
      </Box>
    );
  }

  if (!stock) {
    return (
      <Box sx={{p: 3}}>
        <Alert severity="error">
          {extractErrorMessage(stockQuery.error) || "Не удалось загрузить остатки ячейки"}
        </Alert>
      </Box>
    );
  }

  const stockRows = [
    ...stock.standard.map((s) => ({
      key: `s:${s.catalogItemId}`,
      name: s.catalogItemName,
      expected: s.expected,
      catalogItemId: s.catalogItemId,
      catalogItemName: s.catalogItemName,
      isArchived: s.catalogItem?.isArchived ?? false,
    })),
    ...stock.units.map((u) => ({
      key: `u:${u.unitInventoryItemId}`,
      name: `${u.catalogItemName} — инв. № ${u.inventoryNumber}`,
      expected: 1,
      catalogItemId: u.catalogItemId,
      catalogItemName: u.catalogItemName,
      isArchived: u.catalogItem?.isArchived ?? false,
      inventoryNumber: u.inventoryNumber,
    })),
  ].sort(compareDraftRows);

  const rows: SheetRow[] = [
    ...stockRows,
    ...Array.from({length: BLANK_ROWS}, (_, i) => ({key: `blank:${i}`, name: "", expected: null})),
  ];

  return (
    <Box sx={{bgcolor: "#fff", color: "#000", minHeight: "100vh"}}>
      <GlobalStyles styles={{"@page": {size: "A4 portrait", margin: "10mm"}}} />
      <Box
        sx={{maxWidth: "190mm", mx: "auto", p: "10mm", "@media print": {p: 0, maxWidth: "none"}}}
      >
        <Typography sx={{fontSize: "12pt", fontWeight: 600, mb: "3mm"}}>
          {formatStoragePlaceNodeName(stock.nodePath)}
        </Typography>
        <Box component="table" sx={{width: "100%", borderCollapse: "collapse"}}>
          <Box component="thead" sx={{display: "table-header-group"}}>
            <tr>
              <Box component="th" sx={{...cellSx, width: "10mm"}}>
                №
              </Box>
              <Box component="th" sx={{...cellSx, textAlign: "left"}}>
                Наименование
              </Box>
              <Box component="th" sx={{...cellSx, width: "20mm"}}>
                Учёт
              </Box>
              <Box component="th" sx={{...cellSx, width: "50mm"}}>
                Факт
              </Box>
            </tr>
          </Box>
          <tbody>
            {rows.map((row, i) => (
              <Box component="tr" key={row.key} sx={{breakInside: "avoid"}}>
                <Box component="td" sx={{...cellSx, textAlign: "center"}}>
                  {i + 1}
                </Box>
                <Box component="td" sx={{...cellSx, overflowWrap: "anywhere"}}>
                  {row.name}
                </Box>
                <Box component="td" sx={{...cellSx, textAlign: "center"}}>
                  {row.expected}
                </Box>
                <Box component="td" sx={cellSx} />
              </Box>
            ))}
          </tbody>
        </Box>
      </Box>
    </Box>
  );
}

export default StocktakeNodePrintPage;
