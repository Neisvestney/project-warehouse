import {useState} from "react";
import {Link as RouterLink, useNavigate, useParams} from "react-router";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Divider,
  MenuItem,
  Paper,
  Select,
  Stack,
  Typography,
} from "@mui/material";
import {Controller, useForm} from "react-hook-form";
import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";
import {
  receiptsGetByIdOptions,
  receiptsCancelMutation,
  receiptsDeleteMutation,
  receiptsFinishMutation,
  receiptsPlanMutation,
  receiptsRevertMutation,
  receiptsStartProcessingMutation,
  receiptsUpdateMutation,
} from "@/api/@tanstack/react-query.gen";
import {isNotFoundError} from "@/utils/errorUtils";
import {useHasPermission} from "@/hooks/usePermission";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";
import {FormTextField} from "@/components/form/FormTextField";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import PageGenericHeader from "@/components/PageGenericHeader";
import NotFound from "@/components/NotFound";
import QueryError from "@/components/QueryError";
import ConfirmDialog from "@/components/ConfirmDialog";
import InfoRow from "@/components/InfoRow";
import ReceiptStatusChip from "@/components/receipts/ReceiptStatusChip";
import ReceiptItemsSection from "@/components/receipts/ReceiptItemsSection";
import {RECEIPT_REASON_LABELS, formatReceiptNumber} from "@/components/receipts/receiptUtils";
import type {ReceiptDto, ReceiptReason} from "@/api/types.gen";

const ALL_REASONS: ReceiptReason[] = ["newGoods", "return", "other"];

interface EditInfoFormValues {
  name: string;
  reason: ReceiptReason;
  notes: string;
}

function EditInfoForm({
  receipt,
  onDone,
}: {
  receipt: ReceiptDto;
  onDone: (updated: ReceiptDto) => void;
}) {
  const form = useForm<EditInfoFormValues>({
    defaultValues: {
      name: receipt.name,
      reason: receipt.reason,
      notes: receipt.notes ?? "",
    },
  });
  const {setApiError} = useRhfApiErrors(form);

  const mutation = useMutation({
    ...receiptsUpdateMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: onDone,
    onError: setApiError,
  });

  const onSubmit = form.handleSubmit((values) => {
    mutation.mutate({
      path: {id: receipt.id},
      body: {
        name: values.name,
        reason: values.reason,
        notes: values.notes || null,
      },
    });
  });

  return (
    <Box component="form" onSubmit={onSubmit}>
      <Stack spacing={2}>
        <FormTextField
          control={form.control}
          name="name"
          label="Название"
          rules={{required: "Обязательное поле"}}
          disabled={mutation.isPending}
          fullWidth
        />
        <Controller
          control={form.control}
          name="reason"
          render={({field}) => (
            <Stack spacing={0.5}>
              <Typography variant="body2" color="text.secondary">
                Причина поступления
              </Typography>
              <Select {...field} size="small" fullWidth disabled={mutation.isPending}>
                {ALL_REASONS.map((r) => (
                  <MenuItem key={r} value={r}>
                    {RECEIPT_REASON_LABELS[r]}
                  </MenuItem>
                ))}
              </Select>
            </Stack>
          )}
        />
        <FormTextField
          control={form.control}
          name="notes"
          label="Примечания"
          multiline
          rows={2}
          disabled={mutation.isPending}
          fullWidth
        />
        {form.formState.errors.root && (
          <Alert severity="error">{form.formState.errors.root.message}</Alert>
        )}
        <Stack direction="row" spacing={1} sx={{justifyContent: "flex-end"}}>
          <Button onClick={() => onDone(receipt)} disabled={mutation.isPending}>
            Отмена
          </Button>
          <Button type="submit" variant="contained" disabled={mutation.isPending}>
            {mutation.isPending ? <CircularProgress size={22} color="inherit" /> : "Сохранить"}
          </Button>
        </Stack>
      </Stack>
    </Box>
  );
}

function ReceiptPage() {
  const {id} = useParams<{id: string}>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [isEditing, setIsEditing] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [cancelOpen, setCancelOpen] = useState(false);

  const canEdit = useHasPermission(["receipts.edit", "receipts.edit_assigned"]);

  const queryKey = receiptsGetByIdOptions({path: {id: id!}}).queryKey;

  const {
    data: receipt,
    isLoading,
    isError,
    isRefetchError,
    error,
  } = useQuery({
    ...receiptsGetByIdOptions({path: {id: id!}}),
    meta: {suppressGlobalError: true, suppressGlobalNotFound: true},
  });

  const updateLocalReceipt = (updated: ReceiptDto) => {
    queryClient.setQueryData(queryKey, updated);
  };

  const planMutation = useMutation({
    ...receiptsPlanMutation(),
    onSuccess: updateLocalReceipt,
  });

  const startProcessingMutation = useMutation({
    ...receiptsStartProcessingMutation(),
    onSuccess: updateLocalReceipt,
  });

  const finishMutation = useMutation({
    ...receiptsFinishMutation(),
    onSuccess: updateLocalReceipt,
  });

  const revertMutation = useMutation({
    ...receiptsRevertMutation(),
    onSuccess: updateLocalReceipt,
  });

  const cancelMutation = useMutation({
    ...receiptsCancelMutation(),
    onSuccess: (data) => {
      updateLocalReceipt(data);
      setCancelOpen(false);
    },
  });

  const deleteMutation = useMutation({
    ...receiptsDeleteMutation(),
    onSuccess: () => navigate("/operations/receipts"),
  });

  if (isLoading) {
    return (
      <Box sx={{display: "flex", justifyContent: "center", pt: 8}}>
        <CircularProgress />
      </Box>
    );
  }

  if (isError && !isRefetchError)
    return isNotFoundError(error) ? <NotFound /> : <QueryError error={error} />;
  if (!receipt) return <NotFound />;

  const {status} = receipt;
  const isDraft = status === "draft";
  const isPlanned = status === "planned";
  const isProcessing = status === "processing";
  const isFinished = status === "finished";
  const isTerminal = status === "finished" || status === "canceled";

  const actionPending =
    planMutation.isPending ||
    startProcessingMutation.isPending ||
    finishMutation.isPending ||
    revertMutation.isPending ||
    cancelMutation.isPending ||
    deleteMutation.isPending;

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs
        path={[
          {name: "Приемки", link: "/operations/receipts"},
          {name: formatReceiptNumber(receipt.number)},
        ]}
      />
      <PageGenericHeader
        title={
          <Stack direction="row" spacing={1.5} sx={{alignItems: "center"}}>
            <Typography variant="h5" component="span">
              {receipt.name}
            </Typography>
            <ReceiptStatusChip status={receipt.status} />
          </Stack>
        }
        right={
          canEdit && (!isTerminal || isFinished) ? (
            <Stack direction="row" spacing={1}>
              {isDraft && (
                <>
                  <Button
                    variant="contained"
                    disabled={actionPending}
                    onClick={() => planMutation.mutate({path: {id: receipt.id}})}
                  >
                    {planMutation.isPending ? (
                      <CircularProgress size={20} color="inherit" />
                    ) : (
                      "Запланировать"
                    )}
                  </Button>
                  <Button
                    color="error"
                    variant="outlined"
                    disabled={actionPending}
                    onClick={() => setDeleteOpen(true)}
                  >
                    Удалить
                  </Button>
                </>
              )}
              {isPlanned && (
                <>
                  <Button
                    variant="contained"
                    disabled={actionPending}
                    onClick={() => startProcessingMutation.mutate({path: {id: receipt.id}})}
                  >
                    {startProcessingMutation.isPending ? (
                      <CircularProgress size={20} color="inherit" />
                    ) : (
                      "Начать приёмку"
                    )}
                  </Button>
                  <Button
                    variant="outlined"
                    disabled={actionPending}
                    onClick={() => revertMutation.mutate({path: {id: receipt.id}})}
                  >
                    {revertMutation.isPending ? (
                      <CircularProgress size={20} color="inherit" />
                    ) : (
                      "Откатить"
                    )}
                  </Button>
                </>
              )}
              {isProcessing && (
                <Button
                  variant="contained"
                  disabled={actionPending}
                  onClick={() => finishMutation.mutate({path: {id: receipt.id}})}
                >
                  {finishMutation.isPending ? (
                    <CircularProgress size={20} color="inherit" />
                  ) : (
                    "Завершить"
                  )}
                </Button>
              )}
              {isFinished && (
                <Button
                  variant="outlined"
                  disabled={actionPending}
                  onClick={() => revertMutation.mutate({path: {id: receipt.id}})}
                >
                  {revertMutation.isPending ? (
                    <CircularProgress size={20} color="inherit" />
                  ) : (
                    "Откатить"
                  )}
                </Button>
              )}
              {!isDraft && !isFinished && (
                <Button
                  color="error"
                  variant="outlined"
                  disabled={actionPending}
                  onClick={() => setCancelOpen(true)}
                >
                  Отменить
                </Button>
              )}
            </Stack>
          ) : undefined
        }
      />

      <Paper>
        <Stack spacing={1.5} sx={{p: 3}}>
          {!isEditing ? (
            <>
              <InfoRow label="Номер" value={formatReceiptNumber(receipt.number)} />
              <InfoRow label="Причина" value={RECEIPT_REASON_LABELS[receipt.reason]} />
              <Stack direction="row" spacing={1} sx={{alignItems: "baseline"}}>
                <Typography color="text.secondary" sx={{width: 160, flexShrink: 0}}>
                  Склад
                </Typography>
                <Typography
                  component={RouterLink}
                  to={`/storage/warehouses/${receipt.warehouseId}`}
                  sx={{
                    color: "primary.main",
                    textDecoration: "none",
                    "&:hover": {textDecoration: "underline"},
                  }}
                >
                  {receipt.warehouseName}
                </Typography>
              </Stack>
              <InfoRow
                label="Создана"
                value={new Date(receipt.createdAt).toLocaleString("ru-RU")}
              />
              <InfoRow label="Примечания" value={receipt.notes ?? "—"} />
              {isDraft && canEdit && (
                <Box>
                  <Button
                    startIcon={<span style={{fontSize: 16}}>✎</span>}
                    size="small"
                    onClick={() => setIsEditing(true)}
                  >
                    Редактировать
                  </Button>
                </Box>
              )}
            </>
          ) : (
            <EditInfoForm
              receipt={receipt}
              onDone={(updated) => {
                updateLocalReceipt(updated);
                setIsEditing(false);
              }}
            />
          )}
        </Stack>
      </Paper>

      {(isProcessing || isTerminal) && (
        <Paper>
          <Stack direction="row" divider={<Divider orientation="vertical" flexItem />} sx={{p: 2}}>
            {[
              {label: "Запланировано", value: receipt.totalPlannedCount, color: "text.primary"},
              {label: "Принято", value: receipt.totalReceivedCount, color: "text.primary"},
              {
                label: "Расхождение",
                value:
                  receipt.totalReceivedCount - receipt.totalPlannedCount === 0
                    ? "0"
                    : receipt.totalReceivedCount - receipt.totalPlannedCount > 0
                      ? `+${receipt.totalReceivedCount - receipt.totalPlannedCount}`
                      : String(receipt.totalReceivedCount - receipt.totalPlannedCount),
                color:
                  receipt.totalReceivedCount === receipt.totalPlannedCount
                    ? "success.main"
                    : receipt.totalReceivedCount < receipt.totalPlannedCount
                      ? "warning.main"
                      : "info.main",
              },
            ].map(({label, value, color}) => (
              <Stack key={label} spacing={0.25} sx={{flexGrow: 1, alignItems: "center", py: 0.5}}>
                <Typography variant="caption" color="text.secondary">
                  {label}
                </Typography>
                <Typography variant="h6" sx={{color, fontWeight: 600}}>
                  {value}
                </Typography>
              </Stack>
            ))}
          </Stack>
        </Paper>
      )}

      <Paper>
        <Box sx={{p: 3}}>
          <Divider sx={{mb: 2}} />
          <ReceiptItemsSection receipt={receipt} onUpdate={updateLocalReceipt} />
        </Box>
      </Paper>

      <ConfirmDialog
        open={deleteOpen}
        onClose={() => setDeleteOpen(false)}
        title="Удалить приемку?"
        confirmText="Удалить"
        confirmColor="error"
        onConfirm={() => deleteMutation.mutate({path: {id: receipt.id}})}
        isPending={deleteMutation.isPending}
      >
        <Typography>
          Приемка {formatReceiptNumber(receipt.number)} будет безвозвратно удалена.
        </Typography>
      </ConfirmDialog>

      <ConfirmDialog
        open={cancelOpen}
        onClose={() => setCancelOpen(false)}
        title="Отменить приемку?"
        confirmText="Отменить приемку"
        confirmColor="error"
        onConfirm={() => cancelMutation.mutate({path: {id: receipt.id}})}
        isPending={cancelMutation.isPending}
      >
        <Typography>Статус приемки будет изменён на «Отменена».</Typography>
      </ConfirmDialog>
    </Stack>
  );
}

export default ReceiptPage;
