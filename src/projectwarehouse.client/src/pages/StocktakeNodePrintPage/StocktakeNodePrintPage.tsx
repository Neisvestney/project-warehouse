import {useParams, useSearchParams} from "react-router";
import {useQuery} from "@tanstack/react-query";
import {readPrintWithQuantity} from "@/utils/printUtils";
import {stocktakesGetNodeStockOptions} from "@/api/@tanstack/react-query.gen";
import {formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";
import {compareDraftRows} from "@/components/stocktakes/stocktakeDraft";
import PrintTablePage, {
  type PrintTableColumn,
  type PrintTableSheet,
} from "@/components/print/PrintTablePage";

const NAME_COLUMN: PrintTableColumn = {header: "Наименование", align: "left"};
const EXPECTED_COLUMN: PrintTableColumn = {header: "Учёт", width: "20mm"};
const FACT_COLUMN: PrintTableColumn = {header: "Факт", width: "50mm"};

function StocktakeNodePrintPage() {
  const {id = "", nodeId = ""} = useParams();
  const [searchParams] = useSearchParams();
  const withQuantity = readPrintWithQuantity(searchParams);
  const columns = withQuantity
    ? [NAME_COLUMN, EXPECTED_COLUMN, FACT_COLUMN]
    : [NAME_COLUMN, FACT_COLUMN];
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
      rows: stockRows.map((r) => ({
        key: r.key,
        cells: withQuantity ? [r.name, r.expected, null] : [r.name, null],
      })),
    };
  }

  return (
    <PrintTablePage
      columns={columns}
      sheet={sheet}
      isLoading={stockQuery.isLoading}
      error={stockQuery.error}
      errorMessage="Не удалось загрузить остатки ячейки"
      blankRows={5}
    />
  );
}

export default StocktakeNodePrintPage;
