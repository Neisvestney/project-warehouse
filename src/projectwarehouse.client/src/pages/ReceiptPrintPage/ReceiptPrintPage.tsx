import {useParams, useSearchParams} from "react-router";
import {readPrintWithQuantity, readReceiptPrintItemIds} from "@/utils/printUtils";
import {useQuery} from "@tanstack/react-query";
import {receiptsGetByIdOptions} from "@/api/@tanstack/react-query.gen";
import {formatReceiptNumber} from "@/components/receipts/receiptUtils";
import PrintTablePage, {
  type PrintTableColumn,
  type PrintTableSheet,
} from "@/components/print/PrintTablePage";

const NAME_COLUMN: PrintTableColumn = {header: "Наименование", align: "left"};
const PLAN_COLUMN: PrintTableColumn = {header: "План", width: "20mm"};
const FACT_COLUMN: PrintTableColumn = {header: "Факт", width: "50mm"};

function ReceiptPrintPage() {
  const {id = ""} = useParams();
  const [searchParams] = useSearchParams();
  const itemIds = readReceiptPrintItemIds(searchParams);
  const withQuantity = readPrintWithQuantity(searchParams);
  const columns = withQuantity
    ? [NAME_COLUMN, PLAN_COLUMN, FACT_COLUMN]
    : [NAME_COLUMN, FACT_COLUMN];
  const receiptQuery = useQuery({
    ...receiptsGetByIdOptions({path: {id}}),
    meta: {suppressGlobalError: true},
  });
  const receipt = receiptQuery.data;

  let sheet: PrintTableSheet | undefined;
  if (receipt) {
    const number = formatReceiptNumber(receipt.number);
    sheet = {
      title: `${receipt.name ? `${number} — ${receipt.name}` : number} · ${receipt.warehouseName}`,
      rows: receipt.items
        .filter((item) => !itemIds || itemIds.has(item.id))
        .map((item) => ({
          key: item.id,
          cells: withQuantity
            ? [item.catalogItem.fullName, item.plannedCount, null]
            : [item.catalogItem.fullName, null],
        })),
    };
  }

  return (
    <PrintTablePage
      columns={columns}
      sheet={sheet}
      isLoading={receiptQuery.isLoading}
      error={receiptQuery.error}
      errorMessage="Не удалось загрузить приёмку"
      blankRows={5}
    />
  );
}

export default ReceiptPrintPage;
