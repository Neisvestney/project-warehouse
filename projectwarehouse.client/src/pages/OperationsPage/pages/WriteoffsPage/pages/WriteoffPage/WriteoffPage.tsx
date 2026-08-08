import {useState} from "react";
import {Link as RouterLink, useNavigate, useParams} from "react-router";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  MenuItem,
  Paper,
  Select,
  Stack,
  Typography,
} from "@mui/material";
import {Controller, useForm} from "react-hook-form";
import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";
import {
  writeoffsGetByIdOptions,
  writeoffsCancelMutation,
  writeoffsDeleteMutation,
  writeoffsFinishMutation,
  writeoffsUpdateMutation,
} from "@/api/@tanstack/react-query.gen";
import {extractErrorMessage, isNotFoundError} from "@/utils/errorUtils";
import {useHasPermission} from "@/hooks/usePermission";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";
import {FormTextField} from "@/components/form/FormTextField";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import PageGenericHeader from "@/components/PageGenericHeader";
import NotFound from "@/components/NotFound";
import QueryError from "@/components/QueryError";
import ConfirmDialog from "@/components/ConfirmDialog";
import InfoRow from "@/components/InfoRow";
import WriteoffStatusChip from "@/components/writeoffs/WriteoffStatusChip";
import WriteoffItemsSection from "@/components/writeoffs/WriteoffItemsSection";
import {WRITEOFF_REASON_LABELS, formatWriteoffNumber} from "@/components/writeoffs/writeoffUtils";
import type {WriteoffDto, WriteoffReason} from "@/api/types.gen";
import DeleteIcon from "@mui/icons-material/Delete";
import EditIcon from "@mui/icons-material/Edit";
import TaskAltIcon from "@mui/icons-material/TaskAlt";
import BlockIcon from "@mui/icons-material/Block";
import SaveIcon from "@mui/icons-material/Save";
import {useSnackbar} from "notistack";
import {pluralCount} from "@/utils/pluralUtils";

const ALL_REASONS: WriteoffReason[] = ["loss", "defect", "other"];

interface EditInfoFormValues {
  name: string;
  reason: WriteoffReason;
  notes: string;
}

function EditInfoForm({
  writeoff,
  onDone,
}: {
  writeoff: WriteoffDto;
  onDone: (updated: WriteoffDto) => void;
}) {
  const form = useForm<EditInfoFormValues>({
    defaultValues: {
      name: writeoff.name,
      reason: writeoff.reason,
      notes: writeoff.notes ?? "",
    },
  });
  const {setApiError} = useRhfApiErrors(form);

  const mutation = useMutation({
    ...writeoffsUpdateMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: onDone,
    onError: setApiError,
  });

  const onSubmit = form.handleSubmit((values) => {
    mutation.mutate({
      path: {id: writeoff.id},
      body: {name: values.name, reason: values.reason, notes: values.notes || null},
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
                Причина списания
              </Typography>
              <Select {...field} size="small" fullWidth disabled={mutation.isPending}>
                {ALL_REASONS.map((r) => (
                  <MenuItem key={r} value={r}>
                    {WRITEOFF_REASON_LABELS[r]}
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
          <Button onClick={() => onDone(writeoff)} disabled={mutation.isPending}>
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

function pluralItems(n: number): string {
  return pluralCount(n, {
    one: "позиция будет удалена",
    few: "позиции будут удалены",
    many: "позиций будут удалены",
  });
}

function WriteoffPage() {
  const {id} = useParams<{id: string}>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const {enqueueSnackbar} = useSnackbar();
  const [isEditing, setIsEditing] = useState(false);
  const [finishOpen, setFinishOpen] = useState(false);
  const [cancelOpen, setCancelOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);

  const canEdit = useHasPermission(["writeoffs.edit", "writeoffs.edit_assigned"]);

  const queryOptions = writeoffsGetByIdOptions({path: {id: id!}});

  const {
    data: writeoff,
    isLoading,
    isError,
    isRefetchError,
    error,
  } = useQuery({
    ...queryOptions,
    gcTime: 0,
    meta: {suppressGlobalError: true, suppressGlobalNotFound: true},
  });

  const updateLocal = (updated: WriteoffDto) => {
    queryClient.setQueryData(queryOptions.queryKey, updated);
  };

  const finishMutation = useMutation({
    ...writeoffsFinishMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data) => {
      updateLocal(data);
      setFinishOpen(false);
    },
  });

  const cancelMutation = useMutation({
    ...writeoffsCancelMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data) => {
      updateLocal(data);
      setCancelOpen(false);
    },
    onError: (err) =>
      enqueueSnackbar(extractErrorMessage(err) || "Не удалось отменить списание", {
        variant: "error",
      }),
  });

  const deleteMutation = useMutation({
    ...writeoffsDeleteMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: () => navigate("/operations/writeoffs"),
    onError: (err) =>
      enqueueSnackbar(extractErrorMessage(err) || "Не удалось удалить списание", {
        variant: "error",
      }),
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
  if (!writeoff) return <NotFound />;

  const isDraft = writeoff.status === "draft";
  const isTerminal = writeoff.status === "finished" || writeoff.status === "canceled";
  const actionPending =
    finishMutation.isPending || cancelMutation.isPending || deleteMutation.isPending;

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs
        path={[
          {name: "Списания", link: "/operations/writeoffs"},
          {name: formatWriteoffNumber(writeoff.number)},
        ]}
      />
      <PageGenericHeader
        title={
          <Stack direction="row" spacing={1.5} sx={{alignItems: "center"}}>
            <Typography variant="h5" component="span">
              {writeoff.name}
            </Typography>
            <WriteoffStatusChip status={writeoff.status} />
          </Stack>
        }
        right={
          canEdit && !isTerminal ? (
            <Stack direction="row" spacing={1}>
              <Button
                variant="contained"
                color="success"
                disabled={actionPending || writeoff.items.length === 0}
                onClick={() => setFinishOpen(true)}
                startIcon={<TaskAltIcon />}
                loading={finishMutation.isPending}
              >
                Завершить
              </Button>
              <Button
                color="error"
                variant="outlined"
                disabled={actionPending}
                onClick={() => setCancelOpen(true)}
                startIcon={<BlockIcon />}
              >
                Отменить
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
            </Stack>
          ) : undefined
        }
      />

      <Paper>
        <Stack spacing={1.5} sx={{p: 3}}>
          {!isEditing ? (
            <>
              <InfoRow label="Номер" value={formatWriteoffNumber(writeoff.number)} />
              <InfoRow label="Причина" value={WRITEOFF_REASON_LABELS[writeoff.reason]} />
              <Stack direction="row" spacing={1} sx={{alignItems: "baseline"}}>
                <Typography color="text.secondary" sx={{width: 160, flexShrink: 0}}>
                  Склад
                </Typography>
                <Typography
                  component={RouterLink}
                  to={`/storage/warehouses/${writeoff.warehouseId}`}
                  sx={{
                    color: "primary.main",
                    textDecoration: "none",
                    "&:hover": {textDecoration: "underline"},
                  }}
                >
                  {writeoff.warehouseName}
                </Typography>
              </Stack>
              <InfoRow
                label="Создано"
                value={new Date(writeoff.createdAt).toLocaleString("ru-RU")}
              />
              <InfoRow label="Примечания" value={writeoff.notes ?? "—"} />
              {isDraft && canEdit && (
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
              writeoff={writeoff}
              onDone={(updated) => {
                updateLocal(updated);
                setIsEditing(false);
              }}
            />
          )}
        </Stack>
      </Paper>

      <WriteoffItemsSection writeoff={writeoff} />

      <ConfirmDialog
        open={finishOpen}
        onClose={() => setFinishOpen(false)}
        title="Выполнить списание?"
        onConfirm={() => finishMutation.mutate({path: {id: writeoff.id}})}
        isPending={finishMutation.isPending}
        confirmText="Завершить"
        confirmColor="success"
      >
        <Typography>
          {pluralItems(writeoff.items.length)} из склада. Это действие необратимо.
        </Typography>
        {finishMutation.isError && (
          <Alert severity="error" sx={{mt: 1}}>
            {extractErrorMessage(finishMutation.error)}
          </Alert>
        )}
      </ConfirmDialog>

      <ConfirmDialog
        open={cancelOpen}
        onClose={() => setCancelOpen(false)}
        title="Отменить списание?"
        onConfirm={() => cancelMutation.mutate({path: {id: writeoff.id}})}
        isPending={cancelMutation.isPending}
        confirmText="Отменить списание"
        confirmColor="error"
      >
        Документ будет отменён. Товары не будут затронуты.
      </ConfirmDialog>

      <ConfirmDialog
        open={deleteOpen}
        onClose={() => setDeleteOpen(false)}
        title="Удалить списание?"
        onConfirm={() => deleteMutation.mutate({path: {id: writeoff.id}})}
        isPending={deleteMutation.isPending}
        confirmText="Удалить"
        confirmColor="error"
      >
        Документ будет удалён безвозвратно.
      </ConfirmDialog>
    </Stack>
  );
}

export default WriteoffPage;
