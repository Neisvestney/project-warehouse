import {useParams} from "react-router";
import {useQuery} from "@tanstack/react-query";
import {stocktakesGetNodeStockOptions} from "@/api/@tanstack/react-query.gen";
import {formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";
import {compareDraftRows} from "@/components/stocktakes/stocktakeDraft";
import PrintTablePage, {
  type PrintTableColumn,
  type PrintTableSheet,
} from "@/components/print/PrintTablePage";

const COLUMNS: PrintTableColumn[] = [
  {header: "Наименование", align: "left"},
  {header: "Учёт", width: "20mm"},
  {header: "Факт", width: "50mm"},
];

function StocktakeNodePrintPage() {
  const {id = "", nodeId = ""} = useParams();
  const stockQuery = useQuery({
    ...stocktakesGetNodeStockOptions({path: {id, nodeId}}),
    meta: {suppressGlobalError: true},
  });
  const stock = stockQuery.data;

  let sheet: PrintTableSheet | undefined;
  if (stock) {
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

    sheet = {
      title: formatStoragePlaceNodeName(stock.nodePath),
      rows: stockRows.map((r) => ({key: r.key, cells: [r.name, r.expected, null]})),
    };
  }

  return (
    <PrintTablePage
      columns={COLUMNS}
      sheet={sheet}
      isLoading={stockQuery.isLoading}
      error={stockQuery.error}
      errorMessage="Не удалось загрузить остатки ячейки"
      blankRows={5}
    />
  );
}

export default StocktakeNodePrintPage;
