import {useCallback, useState} from "react";
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
import {byOperation} from "@/utils/queryKeys";
import EditLockBanner from "@/components/EditLockBanner";
import StaleDataBanner from "@/components/StaleDataBanner";
import {useEditLock} from "@/hooks/useEditLock";
import LoadingOverlay from "@/components/LoadingOverlay";
import PageGenericHeader from "@/components/PageGenericHeader";
import NotFound from "@/components/NotFound";
import QueryError from "@/components/QueryError";
import ConfirmDialog from "@/components/ConfirmDialog";
import InfoRow from "@/components/InfoRow";
import ReceiptStatusChip from "@/components/receipts/ReceiptStatusChip";
import ReceiptItemsSection from "@/components/receipts/ReceiptItemsSection";
import {RECEIPT_REASON_LABELS, formatReceiptNumber} from "@/components/receipts/receiptUtils";
import type {ReceiptDto, ReceiptReason} from "@/api/types.gen";
import {parseDateOnly} from "@/utils/dateOnly";
import DeleteIcon from "@mui/icons-material/Delete";
import ScheduleSendIcon from "@mui/icons-material/ScheduleSend";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import UndoIcon from "@mui/icons-material/Undo";
import TaskAltIcon from "@mui/icons-material/TaskAlt";
import BlockIcon from "@mui/icons-material/Block";
import SaveIcon from "@mui/icons-material/Save";

const ALL_REASONS: ReceiptReason[] = ["newGoods", "return", "other"];

interface EditInfoFormValues {
  name: string;
  reason: ReceiptReason;
  notes: string;
  plannedDeliveryDate: string;
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
      name: receipt.name ?? "",
      reason: receipt.reason,
      notes: receipt.notes ?? "",
      plannedDeliveryDate: receipt.plannedDeliveryDate ?? "",
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
        name: values.name || null,
        reason: values.reason,
        notes: values.notes || null,
        plannedDeliveryDate: values.plannedDeliveryDate || null,
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
          name="plannedDeliveryDate"
          label="Планируемая дата поставки"
          type="date"
          disabled={mutation.isPending}
          fullWidth
          slotProps={{inputLabel: {shrink: true}}}
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
          <Button
            type="submit"
            variant="contained"
            disabled={mutation.isPending}
            startIcon={<SaveIcon />}
            loading={mutation.isPending}
          >
            Сохранить
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
  const [isEditingItems, setIsEditingItems] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [cancelOpen, setCancelOpen] = useState(false);
  const [startProcessingOpen, setStartProcessingOpen] = useState(false);

  const canEdit = useHasPermission(["receipts.edit", "receipts.edit_assigned"]);

  const queryKey = receiptsGetByIdOptions({path: {id: id!}}).queryKey;

  const {
    data: receipt,
    isLoading,
    isFetching,
    isError,
    isRefetchError,
    error,
    dataUpdatedAt,
  } = useQuery({
    ...receiptsGetByIdOptions({path: {id: id!}}),
    gcTime: 0,
    meta: {suppressGlobalError: true, suppressGlobalNotFound: true},
  });

  const updateLocalReceipt = (updated: ReceiptDto) => {
    queryClient.setQueryData(queryKey, updated);
  };

  // Mutations write the DTO straight back, so refreshing has to invalidate rather than reuse that path.
  const refreshReceipt = useCallback(() => {
    void queryClient.invalidateQueries({
      queryKey: byOperation("receiptsGetById", {path: {id: id!}}),
    });
  }, [queryClient, id]);

  // Editing the items counts too — that editor is a longer sitting than the info form it sits under.
  const isEditingAnything = isEditing || isEditingItems;

  const lock = useEditLock("receipt", id, {
    isDirty: isEditingAnything,
    dataUpdatedAt,
    isFetching,
    isLoading,
    onRefresh: refreshReceipt,
    enabled: isEditingAnything && canEdit,
  });

  const planMutation = useMutation({
    ...receiptsPlanMutation(),
    onSuccess: updateLocalReceipt,
  });

  const startProcessingMutation = useMutation({
    ...receiptsStartProcessingMutation(),
    onSuccess: (data) => {
      updateLocalReceipt(data);
      setStartProcessingOpen(false);
    },
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
    <Box sx={{position: "relative"}}>
      <LoadingOverlay open={lock.showLoadingOverlay && !isEditingAnything} />
      <Stack spacing={2}>
        <EditLockBanner heldBy={lock.heldBy} />
        <StaleDataBanner
          isStale={!lock.heldBy && lock.isStale}
          staleBy={lock.staleBy}
          onRefresh={lock.refresh}
          onDismiss={lock.dismissStale}
        />

        <AppBreadcrumbs
          path={[
            {name: "Приемки", link: "/operations/receipts"},
            {name: formatReceiptNumber(receipt.number)},
          ]}
          viewersOf={{entityType: "receipt", entityId: id}}
        />
        <PageGenericHeader
          title={
            <Stack direction="row" spacing={1.5} sx={{alignItems: "center"}}>
              <Typography variant="h5" component="span">
                {receipt.name || "—"}
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
                      variant="outlined"
                      disabled={actionPending}
                      onClick={() => planMutation.mutate({path: {id: receipt.id}})}
                      startIcon={<ScheduleSendIcon />}
                      loading={planMutation.isPending}
                    >
                      Запланировать
                    </Button>
                    <Button
                      color="error"
                      variant="outlined"
                      disabled={actionPending}
                      onClick={() => setDeleteOpen(true)}
                      startIcon={<DeleteIcon />}
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
                      onClick={() => setStartProcessingOpen(true)}
                      startIcon={<PlayArrowIcon />}
                      loading={startProcessingMutation.isPending}
                    >
                      Начать приёмку
                    </Button>
                    <Button
                      variant="outlined"
                      disabled={actionPending}
                      onClick={() => revertMutation.mutate({path: {id: receipt.id}})}
                      startIcon={<UndoIcon />}
                      loading={revertMutation.isPending}
                    >
                      Откатить
                    </Button>
                  </>
                )}
                {isProcessing && (
                  <Button
                    variant="contained"
                    disabled={actionPending}
                    onClick={() => finishMutation.mutate({path: {id: receipt.id}})}
                    startIcon={<TaskAltIcon />}
                    loading={finishMutation.isPending}
                  >
                    Завершить
                  </Button>
                )}
                {isFinished && (
                  <Button
                    variant="outlined"
                    disabled={actionPending}
                    onClick={() => revertMutation.mutate({path: {id: receipt.id}})}
                    startIcon={<UndoIcon />}
                    loading={revertMutation.isPending}
                  >
                    Откатить
                  </Button>
                )}
                {!isDraft && !isFinished && (
                  <Button
                    color="error"
                    variant="outlined"
                    disabled={actionPending}
                    onClick={() => setCancelOpen(true)}
                    startIcon={<BlockIcon />}
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
                <InfoRow
                  label="Дата поставки"
                  value={
                    receipt.plannedDeliveryDate
                      ? parseDateOnly(receipt.plannedDeliveryDate).toLocaleDateString("ru-RU")
                      : "—"
                  }
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
            <Stack
              direction="row"
              divider={<Divider orientation="vertical" flexItem />}
              sx={{p: 2}}
            >
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
            <ReceiptItemsSection
              receipt={receipt}
              onUpdate={updateLocalReceipt}
              onEditingChange={setIsEditingItems}
            />
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

        <ConfirmDialog
          open={startProcessingOpen}
          onClose={() => setStartProcessingOpen(false)}
          title="Начать приёмку?"
          confirmText="Начать приёмку"
          onConfirm={() => startProcessingMutation.mutate({path: {id: receipt.id}})}
          isPending={startProcessingMutation.isPending}
        >
          <Typography>
            После начала приёмки изменить список позиций будет нельзя. Продолжить?
          </Typography>
        </ConfirmDialog>
      </Stack>
    </Box>
  );
}

export default ReceiptPage;
