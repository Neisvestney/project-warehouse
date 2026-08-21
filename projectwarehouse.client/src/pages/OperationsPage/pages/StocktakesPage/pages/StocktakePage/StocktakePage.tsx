import {useCallback, useState} from "react";
import {Link as RouterLink, useNavigate, useParams} from "react-router";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  MenuItem,
  Paper,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import {Controller, useForm, useWatch} from "react-hook-form";
import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";
import {
  stocktakesCancelMutation,
  stocktakesDeleteMutation,
  stocktakesGetByIdOptions,
  stocktakesRevertMutation,
  stocktakesStartMutation,
  stocktakesScheduleMutation,
  stocktakesToDraftMutation,
  stocktakesUpdateMutation,
} from "@/api/@tanstack/react-query.gen";
import {extractErrorMessage, isNotFoundError} from "@/utils/errorUtils";
import {useHasPermission} from "@/hooks/usePermission";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";
import {FormTextField} from "@/components/form/FormTextField";
import {byOperation} from "@/utils/queryKeys";
import {useEditLock} from "@/hooks/useEditLock";
import LoadingOverlay from "@/components/LoadingOverlay";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import EditLockBanner from "@/components/EditLockBanner";
import StaleDataBanner from "@/components/StaleDataBanner";
import PageGenericHeader from "@/components/PageGenericHeader";
import NotFound from "@/components/NotFound";
import QueryError from "@/components/QueryError";
import ConfirmDialog from "@/components/ConfirmDialog";
import InfoRow from "@/components/InfoRow";
import StocktakeStatusChip from "@/components/stocktakes/StocktakeStatusChip";
import StocktakeNodesSection from "@/components/stocktakes/StocktakeNodesSection";
import StocktakeCountingSection from "@/components/stocktakes/StocktakeCountingSection";
import StocktakeResultSection from "@/components/stocktakes/StocktakeResultSection";
import StocktakeDifferencesDialog from "@/components/stocktakes/StocktakeDifferencesDialog";
import {STOCKTAKE_TYPE_LABELS, formatStocktakeNumber} from "@/components/stocktakes/stocktakeUtils";
import type {StocktakeDto, StocktakeType} from "@/api/types.gen";
import {parseDateOnly} from "@/utils/dateOnly";
import DeleteIcon from "@mui/icons-material/Delete";
import EventIcon from "@mui/icons-material/Event";
import EditNoteIcon from "@mui/icons-material/EditNote";
import EditIcon from "@mui/icons-material/Edit";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import TaskAltIcon from "@mui/icons-material/TaskAlt";
import UndoIcon from "@mui/icons-material/Undo";
import BlockIcon from "@mui/icons-material/Block";
import SaveIcon from "@mui/icons-material/Save";
import {useSnackbar} from "notistack";

interface EditInfoFormValues {
  name: string;
  notes: string;
  type: StocktakeType;
  plannedDate: string;
}

function EditInfoForm({
  stocktake,
  onDone,
}: {
  stocktake: StocktakeDto;
  onDone: (updated: StocktakeDto) => void;
}) {
  const form = useForm<EditInfoFormValues>({
    defaultValues: {
      name: stocktake.name ?? "",
      notes: stocktake.notes ?? "",
      type: stocktake.type,
      plannedDate: stocktake.plannedDate ?? "",
    },
  });
  const {setApiError} = useRhfApiErrors(form);
  const type = useWatch({control: form.control, name: "type"});
  const canEditPlanning = stocktake.status === "planned" || stocktake.status === "draft";

  const mutation = useMutation({
    ...stocktakesUpdateMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: onDone,
    onError: setApiError,
  });

  const onSubmit = form.handleSubmit((values) => {
    mutation.mutate({
      path: {id: stocktake.id},
      body: {
        name: values.name || null,
        notes: values.notes || null,
        // planning is frozen once counting starts — don't send it at all then
        ...(canEditPlanning
          ? {
              type: values.type,
              plannedDate: values.type === "scheduled" ? values.plannedDate || null : null,
            }
          : {}),
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
        {canEditPlanning && (
          <Controller
            control={form.control}
            name="type"
            render={({field}) => (
              <TextField {...field} select label="Тип" disabled={mutation.isPending} fullWidth>
                {(Object.keys(STOCKTAKE_TYPE_LABELS) as StocktakeType[]).map((t) => (
                  <MenuItem key={t} value={t}>
                    {STOCKTAKE_TYPE_LABELS[t]}
                  </MenuItem>
                ))}
              </TextField>
            )}
          />
        )}
        {canEditPlanning && type === "scheduled" && (
          <FormTextField
            control={form.control}
            name="plannedDate"
            rules={{required: "Обязательное поле"}}
            label="Плановая дата"
            type="date"
            disabled={mutation.isPending}
            fullWidth
            slotProps={{inputLabel: {shrink: true}}}
          />
        )}
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
          <Button onClick={() => onDone(stocktake)} disabled={mutation.isPending}>
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

function StocktakePage() {
  const {id} = useParams<{id: string}>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const {enqueueSnackbar} = useSnackbar();
  const [isEditing, setIsEditing] = useState(false);
  const [isEditingNodes, setIsEditingNodes] = useState(false);
  const [differencesOpen, setDifferencesOpen] = useState(false);
  const [cancelOpen, setCancelOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);

  const canEdit = useHasPermission(["stocktakes.edit", "stocktakes.edit_assigned"]);

  const queryOptions = stocktakesGetByIdOptions({path: {id: id!}});

  const {
    data: stocktake,
    isLoading,
    isFetching,
    isError,
    isRefetchError,
    error,
    dataUpdatedAt,
  } = useQuery({
    ...queryOptions,
    gcTime: 0,
    meta: {suppressGlobalError: true, suppressGlobalNotFound: true},
  });

  const updateLocal = (updated: StocktakeDto) => {
    queryClient.setQueryData(queryOptions.queryKey, updated);
  };

  // Mutations write the DTO straight back, so refreshing has to invalidate rather than reuse that path.
  const refreshStocktake = useCallback(() => {
    void queryClient.invalidateQueries({
      queryKey: byOperation("stocktakesGetById", {path: {id: id!}}),
    });
  }, [queryClient, id]);

  // Counting is deliberately not a locking mode: cells are saved one by one and several people
  // counting different cells of the same stocktake is the normal case, not a collision.
  const isEditingAnything = isEditing || isEditingNodes;

  const lock = useEditLock("stocktake", id, {
    isDirty: isEditingAnything,
    dataUpdatedAt,
    onRefresh: refreshStocktake,
    enabled: isEditingAnything && canEdit,
  });

  const notifyError = (fallback: string) => (err: unknown) =>
    enqueueSnackbar(extractErrorMessage(err) || fallback, {variant: "error"});

  const scheduleMutation = useMutation({
    ...stocktakesScheduleMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: updateLocal,
    onError: notifyError("Не удалось запланировать инвентаризацию"),
  });

  const toDraftMutation = useMutation({
    ...stocktakesToDraftMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: updateLocal,
    onError: notifyError("Не удалось вернуть инвентаризацию в черновик"),
  });

  const startMutation = useMutation({
    ...stocktakesStartMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: updateLocal,
    onError: notifyError("Не удалось начать инвентаризацию"),
  });

  const revertMutation = useMutation({
    ...stocktakesRevertMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: updateLocal,
    onError: notifyError("Не удалось вернуть в черновик"),
  });

  const cancelMutation = useMutation({
    ...stocktakesCancelMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data) => {
      updateLocal(data);
      setCancelOpen(false);
    },
    onError: notifyError("Не удалось отменить инвентаризацию"),
  });

  const deleteMutation = useMutation({
    ...stocktakesDeleteMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: () => navigate("/operations/stocktakes"),
    onError: notifyError("Не удалось удалить инвентаризацию"),
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
  if (!stocktake) return <NotFound />;

  const isPlanned = stocktake.status === "planned";
  const isDraft = stocktake.status === "draft";
  const isInProgress = stocktake.status === "inProgress";
  const isTerminal = stocktake.status === "finished" || stocktake.status === "canceled";
  const canSchedule = isDraft && stocktake.type === "scheduled" && !!stocktake.plannedDate;
  const actionPending =
    scheduleMutation.isPending ||
    toDraftMutation.isPending ||
    startMutation.isPending ||
    revertMutation.isPending ||
    cancelMutation.isPending ||
    deleteMutation.isPending;

  return (
    <Box sx={{position: "relative"}}>
      <LoadingOverlay open={isFetching && !isLoading && !isEditingAnything} />
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
            {name: "Инвентаризации", link: "/operations/stocktakes"},
            {name: formatStocktakeNumber(stocktake.number)},
          ]}
          viewersOf={{entityType: "stocktake", entityId: id}}
        />
        <PageGenericHeader
          title={
            <Stack direction="row" spacing={1.5} sx={{alignItems: "center"}}>
              <Typography variant="h5" component="span">
                {stocktake.name || "—"}
              </Typography>
              <StocktakeStatusChip status={stocktake.status} />
            </Stack>
          }
          right={
            canEdit && !isTerminal ? (
              <Stack direction="row" spacing={1}>
                {(isDraft || isPlanned) && (
                  <Button
                    variant="contained"
                    disabled={actionPending || stocktake.nodes.length === 0}
                    onClick={() => startMutation.mutate({path: {id: stocktake.id}})}
                    startIcon={<PlayArrowIcon />}
                    loading={startMutation.isPending}
                  >
                    Начать
                  </Button>
                )}
                {canSchedule && (
                  <Button
                    variant="outlined"
                    disabled={actionPending || stocktake.nodes.length === 0}
                    onClick={() => scheduleMutation.mutate({path: {id: stocktake.id}})}
                    startIcon={<EventIcon />}
                    loading={scheduleMutation.isPending}
                  >
                    Запланировать
                  </Button>
                )}
                {isPlanned && (
                  <Button
                    variant="outlined"
                    disabled={actionPending}
                    onClick={() => toDraftMutation.mutate({path: {id: stocktake.id}})}
                    startIcon={<EditNoteIcon />}
                    loading={toDraftMutation.isPending}
                  >
                    Вернуть в черновик
                  </Button>
                )}
                {isInProgress && (
                  <>
                    <Button
                      variant="contained"
                      color="success"
                      disabled={actionPending}
                      onClick={() => setDifferencesOpen(true)}
                      startIcon={<TaskAltIcon />}
                    >
                      Завершить
                    </Button>
                    <Button
                      variant="outlined"
                      disabled={actionPending}
                      onClick={() => revertMutation.mutate({path: {id: stocktake.id}})}
                      startIcon={<UndoIcon />}
                      loading={revertMutation.isPending}
                    >
                      В черновик
                    </Button>
                  </>
                )}
                <Button
                  color="error"
                  variant="outlined"
                  disabled={actionPending}
                  onClick={() => setCancelOpen(true)}
                  startIcon={<BlockIcon />}
                >
                  Отменить
                </Button>
                {(isPlanned || isDraft) && (
                  <Button
                    color="error"
                    variant="outlined"
                    disabled={actionPending}
                    onClick={() => setDeleteOpen(true)}
                    startIcon={<DeleteIcon />}
                  >
                    Удалить
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
                <InfoRow label="Номер" value={formatStocktakeNumber(stocktake.number)} />
                <Stack direction="row" spacing={1} sx={{alignItems: "baseline"}}>
                  <Typography color="text.secondary" sx={{width: 160, flexShrink: 0}}>
                    Склад
                  </Typography>
                  <Typography
                    component={RouterLink}
                    to={`/storage/warehouses/${stocktake.warehouseId}`}
                    sx={{
                      color: "primary.main",
                      textDecoration: "none",
                      "&:hover": {textDecoration: "underline"},
                    }}
                  >
                    {stocktake.warehouseName}
                  </Typography>
                </Stack>
                <InfoRow label="Тип" value={STOCKTAKE_TYPE_LABELS[stocktake.type]} />
                {stocktake.type === "scheduled" && (
                  <InfoRow
                    label="Плановая дата"
                    value={
                      stocktake.plannedDate
                        ? parseDateOnly(stocktake.plannedDate).toLocaleDateString("ru-RU")
                        : "—"
                    }
                  />
                )}
                <InfoRow
                  label="Создано"
                  value={new Date(stocktake.createdAt).toLocaleString("ru-RU")}
                />
                <InfoRow
                  label="Начата"
                  value={
                    stocktake.startedAt
                      ? new Date(stocktake.startedAt).toLocaleString("ru-RU")
                      : "—"
                  }
                />
                <InfoRow
                  label="Завершена"
                  value={
                    stocktake.finishedAt
                      ? new Date(stocktake.finishedAt).toLocaleString("ru-RU")
                      : "—"
                  }
                />
                <InfoRow label="Примечания" value={stocktake.notes ?? "—"} />
                {!isTerminal && canEdit && (
                  <Box>
                    <Button
                      startIcon={<EditIcon fontSize="small" />}
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
                stocktake={stocktake}
                onDone={(updated) => {
                  updateLocal(updated);
                  setIsEditing(false);
                }}
              />
            )}
          </Stack>
        </Paper>

        {(isPlanned || isDraft) && (
          <StocktakeNodesSection
            stocktake={stocktake}
            onUpdated={updateLocal}
            onEditingChange={setIsEditingNodes}
          />
        )}
        {isInProgress && (
          <StocktakeCountingSection
            stocktake={stocktake}
            onUpdated={updateLocal}
            onShowDifferences={() => setDifferencesOpen(true)}
          />
        )}
        {isTerminal && <StocktakeResultSection stocktake={stocktake} />}

        <StocktakeDifferencesDialog
          open={differencesOpen}
          stocktake={stocktake}
          onClose={() => setDifferencesOpen(false)}
          onFinished={(updated) => {
            updateLocal(updated);
            setDifferencesOpen(false);
          }}
        />

        <ConfirmDialog
          open={cancelOpen}
          onClose={() => setCancelOpen(false)}
          title="Отменить инвентаризацию?"
          onConfirm={() => cancelMutation.mutate({path: {id: stocktake.id}})}
          isPending={cancelMutation.isPending}
          confirmText="Отменить инвентаризацию"
          confirmColor="error"
        >
          Документ будет отменён. Остатки не будут затронуты.
        </ConfirmDialog>

        <ConfirmDialog
          open={deleteOpen}
          onClose={() => setDeleteOpen(false)}
          title="Удалить инвентаризацию?"
          onConfirm={() => deleteMutation.mutate({path: {id: stocktake.id}})}
          isPending={deleteMutation.isPending}
          confirmText="Удалить"
          confirmColor="error"
        >
          Документ будет удалён безвозвратно.
        </ConfirmDialog>
      </Stack>
    </Box>
  );
}

export default StocktakePage;
