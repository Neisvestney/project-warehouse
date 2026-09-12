import {useMemo, useState} from "react";
import {
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  Paper,
  Stack,
  Tooltip,
  Typography,
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import UndoIcon from "@mui/icons-material/Undo";
import SearchInput from "@/components/SearchInput";
import {useBackClosable} from "@/hooks/useBackClosable";
import {formatPostingNumber} from "@/utils/postingNumberUtils";
import {groupTaskEntries, type BatchGroup} from "./batchGroups";

interface GroupTasksDialogProps {
  open: boolean;
  onClose: () => void;
  group: BatchGroup;
  excludedTaskIds: ReadonlySet<string>;
  onToggleTask: (taskId: string) => void;
}

function GroupTasksDialog({
  open,
  onClose,
  group,
  excludedTaskIds,
  onToggleTask,
}: GroupTasksDialogProps) {
  const [search, setSearch] = useState("");

  useBackClosable(open, onClose);

  const entries = useMemo(() => groupTaskEntries(group), [group]);
  const query = search.trim().toLowerCase();
  const shown = query
    ? entries.filter(
        (e) =>
          (e.orderNumber ?? "").toLowerCase().includes(query) ||
          (e.postingNumber ?? "").toLowerCase().includes(query),
      )
    : entries;

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{pb: 1}}>
        <Stack spacing={0.5}>
          <Typography variant="h6">Задания позиции</Typography>
          <Typography variant="caption" color="text.secondary">
            {group.catalogItemName}
          </Typography>
        </Stack>
      </DialogTitle>

      <DialogContent>
        <Stack spacing={1.5} sx={{mt: 1}}>
          <SearchInput
            value={search}
            onChange={setSearch}
            fullWidth
            label="Номер заказа или отправления"
          />

          {shown.length === 0 && (
            <Typography variant="body2" color="text.secondary">
              Ничего не найдено
            </Typography>
          )}

          {shown.map((entry) => {
            const excluded = excludedTaskIds.has(entry.taskId);
            return (
              <Paper
                key={entry.taskId}
                variant="outlined"
                sx={{p: 1, opacity: excluded ? 0.5 : undefined}}
              >
                <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
                  <Stack sx={{flexGrow: 1, minWidth: 0}}>
                    <Typography
                      variant="body2"
                      sx={{textDecoration: excluded ? "line-through" : undefined}}
                    >
                      {entry.orderNumber ?? "заказ"}
                    </Typography>
                    {entry.postingNumber && (
                      <Typography variant="caption" color="text.secondary">
                        {formatPostingNumber(entry.postingNumber)}
                      </Typography>
                    )}
                  </Stack>
                  {excluded ? (
                    <Chip size="small" label="убрано" />
                  ) : (
                    <Typography variant="caption" color="text.secondary">
                      {entry.qty} шт
                    </Typography>
                  )}
                  <Tooltip title={excluded ? "Вернуть в сборку" : "Убрать из сборки"}>
                    <IconButton size="small" onClick={() => onToggleTask(entry.taskId)}>
                      {excluded ? <UndoIcon fontSize="small" /> : <CloseIcon fontSize="small" />}
                    </IconButton>
                  </Tooltip>
                </Stack>
              </Paper>
            );
          })}
        </Stack>
      </DialogContent>

      <DialogActions>
        <Button onClick={onClose}>Готово</Button>
      </DialogActions>
    </Dialog>
  );
}

export default GroupTasksDialog;
