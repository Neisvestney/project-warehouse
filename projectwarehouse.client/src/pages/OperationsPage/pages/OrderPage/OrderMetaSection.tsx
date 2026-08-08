import {type ReactNode, useState} from "react";
import {Alert, Box, Button, Stack, Typography, Chip} from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import SaveIcon from "@mui/icons-material/Save";
import {useForm} from "react-hook-form";
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {ordersGetByIdQueryKey, ordersUpdateMutation} from "@/api/@tanstack/react-query.gen";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";
import {FormTextField} from "@/components/form/FormTextField";
import type {OrderDetailsDto} from "@/api/types.gen";
import MarketplaceOrderStatusChip from "@/components/orders/marketplace/MarketplaceOrderStatusChip";
import {format} from "date-fns";
import {ru} from "date-fns/locale";
import {MARKETPLACE_TYPE_COLORS} from "@/pages/SettingsPage/pages/MarketplacesSettingsPage/marketplaceUtils.ts";
import {Link} from "react-router";

function formatDate(iso: string | null | undefined): string {
  if (!iso) return "—";
  try {
    return format(new Date(iso), "d MMM yyyy, HH:mm", {locale: ru});
  } catch {
    return iso;
  }
}

function toDateTimeLocal(iso: string | null | undefined): string {
  if (!iso) return "";
  try {
    return format(new Date(iso), "yyyy-MM-dd'T'HH:mm");
  } catch {
    return "";
  }
}

function MetaRow({label, children}: {label: string; children: ReactNode}) {
  return (
    <Stack direction="row" spacing={1} sx={{alignItems: "baseline", minHeight: 32}}>
      <Typography color="text.secondary" sx={{width: 160, flexShrink: 0, pt: 0.25}}>
        {label}
      </Typography>
      <Stack sx={{flex: 1}}>{children}</Stack>
    </Stack>
  );
}

interface EditInfoFormValues {
  plannedShipmentAt: string;
  notes: string;
}

function EditInfoForm({order, onDone}: {order: OrderDetailsDto; onDone: () => void}) {
  const queryClient = useQueryClient();
  const form = useForm<EditInfoFormValues>({
    defaultValues: {
      plannedShipmentAt: toDateTimeLocal(order.plannedShipmentAt),
      notes: order.notes ?? "",
    },
  });
  const {setApiError} = useRhfApiErrors(form);

  const mutation = useMutation({
    ...ordersUpdateMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: ordersGetByIdQueryKey({path: {id: order.id}}),
      });
      onDone();
    },
    onError: setApiError,
  });

  const onSubmit = form.handleSubmit((values) => {
    mutation.mutate({
      path: {id: order.id},
      body: {
        plannedShipmentAt: values.plannedShipmentAt || null,
        notes: values.notes.trim() || null,
      },
    });
  });

  return (
    <Box component="form" onSubmit={onSubmit}>
      <Stack spacing={2}>
        <FormTextField
          control={form.control}
          name="plannedShipmentAt"
          label="Плановая отгрузка"
          type="datetime-local"
          disabled={mutation.isPending}
          fullWidth
          slotProps={{inputLabel: {shrink: true}}}
        />
        <FormTextField
          control={form.control}
          name="notes"
          label="Заметки"
          multiline
          rows={2}
          disabled={mutation.isPending}
          fullWidth
        />
        {form.formState.errors.root && (
          <Alert severity="error">{form.formState.errors.root.message}</Alert>
        )}
        <Stack direction="row" spacing={1} sx={{justifyContent: "flex-end"}}>
          <Button onClick={onDone} disabled={mutation.isPending}>
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

interface OrderMetaSectionProps {
  order: OrderDetailsDto;
  canEdit: boolean;
}

function OrderMetaSection({order, canEdit}: OrderMetaSectionProps) {
  const [isEditing, setIsEditing] = useState(false);

  if (isEditing) {
    return <EditInfoForm order={order} onDone={() => setIsEditing(false)} />;
  }

  return (
    <Stack spacing={0.5}>
      <MetaRow label="Склад">
        <Typography variant="body2">{order.warehouseName}</Typography>
      </MetaRow>

      <MetaRow label="Создан">
        <Typography variant="body2">{formatDate(order.createdAt)}</Typography>
      </MetaRow>

      {order.createdByName && (
        <MetaRow label="Создал">
          <Typography variant="body2">{order.createdByName}</Typography>
        </MetaRow>
      )}

      <MetaRow label="Плановая отгрузка">
        <Typography variant="body2">{formatDate(order.plannedShipmentAt)}</Typography>
      </MetaRow>

      <MetaRow label="Заметки">
        <Typography variant="body2">{order.notes || "—"}</Typography>
      </MetaRow>

      {order.marketplaceOrder && (
        <>
          <MetaRow label="Отправление">
            <Typography variant="body2" sx={{fontFamily: "monospace"}}>
              {order.marketplaceOrder.postingNumber}
            </Typography>
          </MetaRow>

          <MetaRow label="Магазин">
            <Box>
              <Chip
                component={Link}
                to={`/settings/integrations/${order.marketplaceOrder.marketplaceAccountId}`}
                size={"small"}
                label={order.marketplaceOrder.marketplaceAccountName}
                color={MARKETPLACE_TYPE_COLORS[order.marketplaceOrder.marketplaceType]}
                onClick={() => {}}
              />
            </Box>
          </MetaRow>

          <MetaRow label="Статус на площадке">
            <Box>
              <MarketplaceOrderStatusChip value={order.marketplaceOrder} />
            </Box>
          </MetaRow>

          {order.marketplaceOrder.trackingNumber && (
            <MetaRow label="Трек-номер">
              <Typography variant="body2" sx={{fontFamily: "monospace"}}>
                {order.marketplaceOrder.trackingNumber}
              </Typography>
            </MetaRow>
          )}

          <MetaRow label="Статус сверен">
            <Typography variant="body2">
              {formatDate(order.marketplaceOrder.statusSyncedAt)}
            </Typography>
          </MetaRow>

          {order.marketplaceOrder.status === "cancelled" && (
            <Alert severity="warning">
              Заказ отменён на маркетплейсе. Сборка в WMS не откатывается автоматически — решение
              принимает человек.
            </Alert>
          )}
        </>
      )}

      {canEdit && (
        <Box sx={{pt: 0.5}}>
          <Button size="small" startIcon={<EditIcon />} onClick={() => setIsEditing(true)}>
            Редактировать
          </Button>
        </Box>
      )}
    </Stack>
  );
}

export default OrderMetaSection;
