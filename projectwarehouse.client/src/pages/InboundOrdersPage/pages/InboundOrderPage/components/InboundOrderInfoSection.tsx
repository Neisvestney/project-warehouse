import {useState} from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Paper,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import {Controller, useForm, useWatch} from "react-hook-form";
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {
  inboundOrdersGetByIdQueryKey,
  inboundOrdersUpdateMutation,
} from "@/api/@tanstack/react-query.gen";
import type {InboundOrderDto} from "@/api/types.gen";
import {INBOUND_ORDER_STATUS_COLORS, INBOUND_ORDER_STATUS_LABELS} from "@/utils/inboundOrderUtils";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";
import {useHasPermission} from "@/hooks/usePermission";
import {FormTextField} from "@/components/form/FormTextField";
import WarehousesSelect from "@/components/WarehousesSelect";
import UsersSelect from "@/components/UsersSelect";
import InfoRow from "@/components/InfoRow";

type EditFormValues = {
  title: string;
  warehouseId: string | null;
  plannedStartDateTime: string;
  notes: string;
  assignedUserIds: string[];
};

function toLocalDateTimeString(isoString: string): string {
  const date = new Date(isoString);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

interface Props {
  order: InboundOrderDto;
}

function InboundOrderInfoSection({order}: Props) {
  const [editing, setEditing] = useState(false);
  const canEdit = useHasPermission([
    "inbound_orders.edit",
    "inbound_orders.edit_assigned_warehouses",
  ]);
  const queryClient = useQueryClient();

  const form = useForm<EditFormValues>({
    defaultValues: {
      title: order.title ?? "",
      warehouseId: order.warehouse.id,
      plannedStartDateTime: toLocalDateTimeString(order.plannedStartDateTime),
      notes: order.notes ?? "",
      assignedUserIds: order.assignedUsers.map((u) => u.id),
    },
  });
  const {setApiError} = useRhfApiErrors(form);
  const warehouseId = useWatch({control: form.control, name: "warehouseId"});

  const mutation = useMutation({
    ...inboundOrdersUpdateMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: inboundOrdersGetByIdQueryKey({path: {id: order.id}}),
      });
      setEditing(false);
    },
    onError: setApiError,
  });

  const onSubmit = form.handleSubmit((values) => {
    mutation.mutate({
      path: {id: order.id},
      body: {
        warehouseId: values.warehouseId!,
        title: values.title || null,
        plannedStartDateTime: new Date(values.plannedStartDateTime).toISOString(),
        notes: values.notes || null,
        assignedUserIds: values.assignedUserIds,
      },
    });
  });

  const handleStartEdit = () => {
    form.reset({
      title: order.title ?? "",
      warehouseId: order.warehouse.id,
      plannedStartDateTime: toLocalDateTimeString(order.plannedStartDateTime),
      notes: order.notes ?? "",
      assignedUserIds: order.assignedUsers.map((u) => u.id),
    });
    setEditing(true);
  };

  const handleCancel = () => {
    setEditing(false);
    form.reset();
  };

  return (
    <Paper>
      <Stack spacing={1.5} sx={{p: 3}}>
        <Stack
          direction="row"
          spacing={1}
          sx={{alignItems: "center", justifyContent: "space-between"}}
        >
          <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
            <Typography variant="subtitle1" sx={{fontWeight: "medium"}}>
              Информация об ордере
            </Typography>
            <Chip
              label={INBOUND_ORDER_STATUS_LABELS[order.status]}
              color={INBOUND_ORDER_STATUS_COLORS[order.status]}
              size="small"
            />
          </Stack>
          {canEdit && !editing && (
            <Button size="small" startIcon={<EditIcon />} onClick={handleStartEdit}>
              Редактировать
            </Button>
          )}
        </Stack>

        {editing ? (
          <Box component="form" onSubmit={onSubmit}>
            <Stack spacing={2.5}>
              <FormTextField
                control={form.control}
                name="title"
                label="Название"
                disabled={mutation.isPending}
                fullWidth
              />
              <Controller
                control={form.control}
                name="warehouseId"
                rules={{required: "Обязательное поле"}}
                render={({field, fieldState}) => (
                  <WarehousesSelect
                    value={field.value}
                    onChange={(id) => {
                      field.onChange(id);
                      form.setValue("assignedUserIds", []);
                    }}
                    disabled={mutation.isPending}
                    textFieldProps={{
                      error: !!fieldState.error,
                      helperText: fieldState.error?.message,
                    }}
                    fullWidth
                  />
                )}
              />
              <Controller
                control={form.control}
                name="plannedStartDateTime"
                rules={{required: "Обязательное поле"}}
                render={({field, fieldState}) => (
                  <TextField
                    {...field}
                    label="Дата начала"
                    type="datetime-local"
                    disabled={mutation.isPending}
                    fullWidth
                    error={!!fieldState.error}
                    helperText={fieldState.error?.message}
                    slotProps={{inputLabel: {shrink: true}}}
                  />
                )}
              />
              <FormTextField
                control={form.control}
                name="notes"
                label="Примечания"
                multiline
                minRows={2}
                disabled={mutation.isPending}
                fullWidth
              />
              <Controller
                control={form.control}
                name="assignedUserIds"
                render={({field}) => (
                  <UsersSelect
                    value={field.value}
                    onChange={field.onChange}
                    warehouseId={warehouseId}
                    disabled={mutation.isPending}
                    fullWidth
                  />
                )}
              />
              {form.formState.errors.root && (
                <Alert severity="error">{form.formState.errors.root.message}</Alert>
              )}
              <Stack direction="row" spacing={1} sx={{justifyContent: "flex-end"}}>
                <Button onClick={handleCancel} disabled={mutation.isPending}>
                  Отмена
                </Button>
                <Button type="submit" variant="contained" disabled={mutation.isPending}>
                  {mutation.isPending ? (
                    <CircularProgress size={22} color="inherit" />
                  ) : (
                    "Сохранить"
                  )}
                </Button>
              </Stack>
            </Stack>
          </Box>
        ) : (
          <>
            <InfoRow
              label="Дата начала"
              value={new Date(order.plannedStartDateTime).toLocaleString("ru-RU", {
                day: "2-digit",
                month: "2-digit",
                year: "numeric",
                hour: "2-digit",
                minute: "2-digit",
              })}
            />
            <InfoRow label="Склад" value={order.warehouse.name} />
            {order.notes && <InfoRow label="Примечания" value={order.notes} />}
            <Stack direction="row" spacing={1} sx={{alignItems: "flex-start"}}>
              <Typography color="text.secondary" sx={{width: 160, flexShrink: 0, pt: 0.25}}>
                Назначенные
              </Typography>
              <Stack direction="row" spacing={0.5} sx={{flexWrap: "wrap", gap: 0.5}}>
                {order.assignedUsers.length > 0 ? (
                  order.assignedUsers.map((u) => (
                    <Chip key={u.id} label={u.username} size="small" />
                  ))
                ) : (
                  <Typography>—</Typography>
                )}
              </Stack>
            </Stack>
          </>
        )}
      </Stack>
    </Paper>
  );
}

export default InboundOrderInfoSection;
