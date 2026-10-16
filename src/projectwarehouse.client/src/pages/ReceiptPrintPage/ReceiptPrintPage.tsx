import {useParams, useSearchParams} from "react-router";
import {readReceiptPrintItemIds} from "@/utils/printUtils";
import {useQuery} from "@tanstack/react-query";
import {receiptsGetByIdOptions} from "@/api/@tanstack/react-query.gen";
import {formatReceiptNumber} from "@/components/receipts/receiptUtils";
import PrintTablePage, {
  type PrintTableColumn,
  type PrintTableSheet,
} from "@/components/print/PrintTablePage";

const COLUMNS: PrintTableColumn[] = [
  {header: "Наименование", align: "left"},
  {header: "План", width: "20mm"},
  {header: "Факт", width: "50mm"},
];

function ReceiptPrintPage() {
  const {id = ""} = useParams();
  const [searchParams] = useSearchParams();
  const itemIds = readReceiptPrintItemIds(searchParams);
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
          cells: [item.catalogItem.fullName, item.plannedCount, null],
        })),
    };
  }

  return (
    <PrintTablePage
      columns={COLUMNS}
      sheet={sheet}
      isLoading={receiptQuery.isLoading}
      error={receiptQuery.error}
      errorMessage="Не удалось загрузить приёмку"
      blankRows={5}
    />
  );
}

export default ReceiptPrintPage;
