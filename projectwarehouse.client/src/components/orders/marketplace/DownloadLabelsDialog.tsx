import {useState} from "react";
import {
  Button,
  Checkbox,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  FormControlLabel,
  FormHelperText,
  FormLabel,
  Radio,
  RadioGroup,
} from "@mui/material";
import {useBackClosable} from "@/hooks/useBackClosable";
import type {OrderLabelsGrouping} from "@/api/types.gen";

const GROUPING_KEY = "orders-labels-grouping";
const GROUPINGS: OrderLabelsGrouping[] = ["none", "article", "accountArticle"];

function loadGrouping(): OrderLabelsGrouping {
  const stored = localStorage.getItem(GROUPING_KEY);
  return GROUPINGS.find((g) => g === stored) ?? "none";
}

interface DownloadLabelsDialogProps {
  open: boolean;
  isPending: boolean;
  onClose: () => void;
  onConfirm: (grouping: OrderLabelsGrouping, forceRegenerate: boolean) => void;
}

function DownloadLabelsDialog({open, isPending, onClose, onConfirm}: DownloadLabelsDialogProps) {
  const [grouping, setGrouping] = useState<OrderLabelsGrouping>(loadGrouping);
  // deliberately not remembered: a stuck flag would refetch from the marketplace on every print
  const [forceRegenerate, setForceRegenerate] = useState(false);

  function changeGrouping(value: OrderLabelsGrouping) {
    setGrouping(value);
    localStorage.setItem(GROUPING_KEY, value);
  }

  useBackClosable(open && !isPending, onClose);

  return (
    <Dialog
      open={open}
      onClose={isPending ? undefined : onClose}
      fullWidth
      maxWidth="xs"
      // resetting on close rather than on confirm keeps the checkbox intact while the request runs
      slotProps={{transition: {onExited: () => setForceRegenerate(false)}}}
    >
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
            <FormControlLabel
              value="accountArticle"
              control={<Radio />}
              label="По магазинам, затем по артикулам"
            />
          </RadioGroup>
        </FormControl>
        <FormControl sx={{mt: 2}}>
          <FormControlLabel
            control={
              <Checkbox
                checked={forceRegenerate}
                onChange={(e) => setForceRegenerate(e.target.checked)}
              />
            }
            label="Перегенерировать этикетки"
          />
          <FormHelperText>
            Этикетки скачиваются с площадки заново и заменяют сохранённые. Работает только для
            заказов в статусе «Ожидает отгрузки».
          </FormHelperText>
        </FormControl>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={isPending}>
          Отмена
        </Button>
        <Button
          variant="contained"
          loading={isPending}
          onClick={() => onConfirm(grouping, forceRegenerate)}
        >
          Скачать
        </Button>
      </DialogActions>
    </Dialog>
  );
}

export default DownloadLabelsDialog;
