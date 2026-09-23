import {useEffect, useRef, useState} from "react";
import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";
import {useSnackbar} from "notistack";
import {
  marketplacesGetAccountQueryKey,
  marketplacesGetBackfillBoundsOptions,
  marketplacesStartSyncMutation,
} from "@/api/@tanstack/react-query.gen";
import {useBackClosable} from "@/hooks/useBackClosable";
import {extractErrorMessage} from "@/utils/errorUtils";
import {addDays, formatDateOnly, parseDateOnly, toDateOnly, todayDateOnly} from "@/utils/dateOnly";

interface BackfillSyncDialogProps {
  open: boolean;
  accountId: string;
  onClose: () => void;
}

/** Тот же день в UTC, каким его набрали: бэкенд режет период по `created_at` отправления. */
function toIsoStart(dateOnly: string): string {
  return parseDateOnly(dateOnly).toISOString();
}

function BackfillSyncDialog({open, accountId, onClose}: BackfillSyncDialogProps) {
  const queryClient = useQueryClient();
  const {enqueueSnackbar} = useSnackbar();

  const [since, setSince] = useState("");
  const [to, setTo] = useState(todayDateOnly());
  const [probe, setProbe] = useState(false);

  const wasOpenRef = useRef(false);
  useEffect(() => {
    if (open && !wasOpenRef.current) {
      setSince("");
      setTo(todayDateOnly());
      setProbe(false);
    }
    wasOpenRef.current = open;
  }, [open]);

  // Без probeMarketplace ответ берётся из базы; запрос с ним уходит только по кнопке
  const bounds = useQuery({
    ...marketplacesGetBackfillBoundsOptions({
      path: {id: accountId},
      query: probe ? {probeMarketplace: true} : undefined,
    }),
    enabled: open,
    meta: {suppressGlobalError: true},
  });

  const mutation = useMutation({
    ...marketplacesStartSyncMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: async () => {
      enqueueSnackbar("Импорт истории запущен", {variant: "success"});
      await queryClient.invalidateQueries({
        queryKey: marketplacesGetAccountQueryKey({path: {id: accountId}}),
      });
      onClose();
    },
    onError: (err) =>
      enqueueSnackbar(extractErrorMessage(err) || "Не удалось запустить импорт", {
        variant: "error",
      }),
  });

  useBackClosable(open && !mutation.isPending, onClose);

  const periodValid = !!since && !!to && parseDateOnly(since) < parseDateOnly(to);
  const firstOrderAt = bounds.data?.firstOrderAt;
  const firstPostingAt = bounds.data?.firstPostingAt;

  return (
    <Dialog open={open} onClose={mutation.isPending ? undefined : onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Импорт истории заказов</DialogTitle>
      <DialogContent>
        <Stack spacing={2.5} sx={{pt: 1}}>
          <Typography variant="body2" color="text.secondary">
            Заказы FBS и FBO за выбранный период заедут как внешние: они не собираются и не
            списывают остатки. Заказы, которые уже есть в системе, импорт не трогает.
          </Typography>

          <Stack direction="row" spacing={2}>
            <TextField
              type="date"
              label="С"
              value={since}
              onChange={(e) => setSince(e.target.value)}
              slotProps={{inputLabel: {shrink: true}}}
              disabled={mutation.isPending}
              fullWidth
            />
            <TextField
              type="date"
              label="По"
              value={to}
              onChange={(e) => setTo(e.target.value)}
              slotProps={{inputLabel: {shrink: true}}}
              disabled={mutation.isPending}
              fullWidth
            />
          </Stack>

          <Stack spacing={1}>
            <Typography variant="body2" color="text.secondary">
              {firstOrderAt
                ? `Первый заказ в системе: ${formatDateOnly(toDateOnly(new Date(firstOrderAt)))}`
                : "Заказов этого магазина в системе ещё нет"}
            </Typography>

            <Stack direction="row" spacing={1} sx={{alignItems: "center"}} useFlexGap>
              <Typography variant="body2" color="text.secondary">
                {firstPostingAt
                  ? `Первое отправление на площадке: ${formatDateOnly(toDateOnly(new Date(firstPostingAt)))}`
                  : "Дата первого отправления на площадке неизвестна"}
              </Typography>
              <Button
                size="small"
                onClick={() => setProbe(true)}
                loading={probe && bounds.isFetching}
                disabled={!!firstPostingAt || mutation.isPending}
              >
                Определить
              </Button>
            </Stack>

            {probe && bounds.isError && (
              <Alert severity="warning">
                {extractErrorMessage(bounds.error) || "Площадка не ответила на запрос"}
              </Alert>
            )}
          </Stack>
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={mutation.isPending}>
          Отмена
        </Button>
        <Button
          variant="contained"
          disabled={!periodValid}
          loading={mutation.isPending}
          onClick={() =>
            mutation.mutate({
              path: {id: accountId},
              body: {
                scope: "ordersBackfill",
                since: toIsoStart(since),
                // конец периода — начало следующего дня, чтобы выбранная дата вошла целиком
                to: toIsoStart(addDays(to, 1)),
              },
            })
          }
        >
          Импортировать
        </Button>
      </DialogActions>
    </Dialog>
  );
}

export default BackfillSyncDialog;
