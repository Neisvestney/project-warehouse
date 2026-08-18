import {useState} from "react";
import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  FormControlLabel,
  FormLabel,
  Radio,
  RadioGroup,
} from "@mui/material";
import type {OrderLabelsGrouping} from "@/api/types.gen";

const GROUPING_KEY = "orders-labels-grouping";

function loadGrouping(): OrderLabelsGrouping {
  return localStorage.getItem(GROUPING_KEY) === "article" ? "article" : "none";
}

interface DownloadLabelsDialogProps {
  open: boolean;
  isPending: boolean;
  onClose: () => void;
  onConfirm: (grouping: OrderLabelsGrouping) => void;
}

function DownloadLabelsDialog({open, isPending, onClose, onConfirm}: DownloadLabelsDialogProps) {
  const [grouping, setGrouping] = useState<OrderLabelsGrouping>(loadGrouping);

  function changeGrouping(value: OrderLabelsGrouping) {
    setGrouping(value);
    localStorage.setItem(GROUPING_KEY, value);
  }

  return (
    <Dialog open={open} onClose={isPending ? undefined : onClose} fullWidth maxWidth="xs">
      <DialogTitle>Скачать этикетки</DialogTitle>
      <DialogContent dividers>
        <FormControl>
          <FormLabel id="labels-grouping-label">Группировать по</FormLabel>
          <RadioGroup
            aria-labelledby="labels-grouping-label"
            value={grouping}
            onChange={(e) => changeGrouping(e.target.value as OrderLabelsGrouping)}
          >
            <FormControlLabel value="none" control={<Radio />} label="Не группировать" />
            <FormControlLabel value="article" control={<Radio />} label="По артикулам" />
          </RadioGroup>
        </FormControl>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={isPending}>
          Отмена
        </Button>
        <Button variant="contained" loading={isPending} onClick={() => onConfirm(grouping)}>
          Скачать
        </Button>
      </DialogActions>
    </Dialog>
  );
}

export default DownloadLabelsDialog;
