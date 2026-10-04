import {useState} from "react";
import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  Radio,
  RadioGroup,
  Stack,
  Typography,
} from "@mui/material";
import PrintIcon from "@mui/icons-material/Print";
import type {CatalogItemSummaryDto} from "@/api/types.gen";
import {ClampedIntegerField} from "@/components/form/ClampedIntegerField";
import {useBackClosable} from "@/hooks/useBackClosable";
import type {BarcodeType} from "@/pages/PrintPage/BarcodeLabel";
import {formatEntityBarcode} from "@/utils/barcodeUtils";
import {NOUNS, pluralCount} from "@/utils/pluralUtils";
import {openPrintPage, type PrintItem} from "@/utils/printUtils";

export type CatalogLabelItem = Pick<
  CatalogItemSummaryDto,
  "id" | "fullName" | "article" | "barcode"
>;

type LabelKind = "internal" | "text" | "barcode";

/** bwip-js rejects EAN13 payloads that are not 12–13 digits, so anything else prints as Code128. */
function barcodeTypeFor(barcode: string): BarcodeType {
  return /^\d{12,13}$/.test(barcode) ? "EAN13" : "Code128";
}

function toPrintItem(item: CatalogLabelItem, kind: LabelKind): PrintItem | null {
  const internal = formatEntityBarcode("catalogItem", item.id);
  switch (kind) {
    case "internal":
      return {
        type: "DataMatrix",
        value: internal,
        label: item.article ? `${item.fullName} · ${item.article}` : item.fullName,
      };
    case "text":
      return {
        type: "DataMatrix",
        value: internal,
        label: item.article ? `${item.fullName}\nАрт. ${item.article}` : item.fullName,
      };
    case "barcode":
      return item.barcode
        ? {
            type: barcodeTypeFor(item.barcode),
            value: item.barcode,
            label: item.article ? `${item.fullName} · ${item.article}` : item.fullName,
          }
        : null;
  }
}

interface CatalogLabelsPrintDialogProps {
  items: CatalogLabelItem[];
  open: boolean;
  onClose: () => void;
}

/** Label print options for one or many catalog items; opens `/print` in a new tab. */
export function CatalogLabelsPrintDialog({items, open, onClose}: CatalogLabelsPrintDialogProps) {
  const [kind, setKind] = useState<LabelKind>("internal");
  const [copies, setCopies] = useState(1);
  const [prevOpen, setPrevOpen] = useState(open);

  if (prevOpen !== open) {
    setPrevOpen(open);
    if (open) {
      setKind("internal");
      setCopies(1);
    }
  }

  useBackClosable(open, onClose);

  const single = items.length === 1 ? items[0] : null;
  const withBarcode = items.filter((i) => i.barcode).length;
  const skipped = kind === "barcode" ? items.length - withBarcode : 0;

  const barcodeLabel = single
    ? single.barcode
      ? `Штрихкод товара — ${single.barcode}`
      : "Штрихкод товара — не заполнен"
    : `Штрихкод товара — есть у ${withBarcode} из ${items.length}`;

  const handlePrint = () => {
    const printItems = items.flatMap((item) => {
      const printItem = toPrintItem(item, kind);
      return printItem ? Array.from({length: copies}, () => printItem) : [];
    });
    openPrintPage(printItems, kind === "text" ? "text" : "barcode");
    onClose();
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>{single ? "Печать этикетки" : "Печать этикеток"}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{pt: 1}}>
          {!single && (
            <Typography variant="body2">
              Выбрано: {pluralCount(items.length, NOUNS.position)}.
            </Typography>
          )}
          <RadioGroup value={kind} onChange={(e) => setKind(e.target.value as LabelKind)}>
            <FormControlLabel
              value="internal"
              control={<Radio size="small" />}
              label="Внутренний код (DataMatrix)"
            />
            <FormControlLabel
              value="text"
              control={<Radio size="small" />}
              label="Название и артикул, маленький DataMatrix"
            />
            <FormControlLabel
              value="barcode"
              control={<Radio size="small" />}
              disabled={withBarcode === 0}
              label={barcodeLabel}
            />
          </RadioGroup>
          {skipped > 0 && (
            <Typography variant="body2" color="warning.main">
              Без штрихкода будет пропущено: {pluralCount(skipped, NOUNS.position)}.
            </Typography>
          )}
          <ClampedIntegerField
            label={single ? "Количество копий" : "Копий на позицию"}
            size="small"
            value={copies}
            max={200}
            onCommit={setCopies}
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Отмена</Button>
        <Button variant="contained" startIcon={<PrintIcon />} onClick={handlePrint}>
          Печать
        </Button>
      </DialogActions>
    </Dialog>
  );
}
