import {useState} from "react";
import {Alert, Box, Button, Stack} from "@mui/material";
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
import MarketplaceAccountChip from "@/components/marketplace/MarketplaceAccountChip";
import InfoRow from "@/components/InfoRow";
import UserChip from "@/components/shared/UserChip";
import WarehouseChip from "@/components/shared/WarehouseChip";
import {formatPostingNumber} from "@/utils/postingNumberUtils";

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
  /** Lifted so the page can tell a stale-data warning apart from a silent refresh. */
  onEditingChange?: (isEditing: boolean) => void;
}

function OrderMetaSection({order, canEdit, onEditingChange}: OrderMetaSectionProps) {
  const [isEditing, setIsEditing] = useState(false);

  function setEditing(value: boolean) {
    setIsEditing(value);
    onEditingChange?.(value);
  }

  if (isEditing) {
    return <EditInfoForm order={order} onDone={() => setEditing(false)} />;
  }

  return (
    <Stack spacing={1.5}>
      <InfoRow
        label="Склад"
        value={<WarehouseChip warehouseId={order.warehouseId} name={order.warehouseName} />}
      />

      <InfoRow label="Создан" value={formatDate(order.createdAt)} />

      {order.createdByName && (
        <InfoRow
          label="Создал"
          value={<UserChip userId={order.createdById} name={order.createdByName} />}
        />
      )}

      <InfoRow label="Плановая отгрузка" value={formatDate(order.plannedShipmentAt)} />
      {order.assembledAt && <InfoRow label="Собран" value={formatDate(order.assembledAt)} />}
      {order.shippedAt && <InfoRow label="Отгружен" value={formatDate(order.shippedAt)} />}

      <InfoRow label="Заметки" value={order.notes || "—"} />

      {order.marketplaceOrder && (
        <>
          <InfoRow
            label="Отправление"
            value={
              <Box component="span" sx={{fontFamily: "monospace"}}>
                {formatPostingNumber(order.marketplaceOrder.postingNumber)}
              </Box>
            }
          />

          <InfoRow
            label="Магазин"
            value={
              <MarketplaceAccountChip
                accountId={order.marketplaceOrder.marketplaceAccountId}
                name={order.marketplaceOrder.marketplaceAccountName}
                type={order.marketplaceOrder.marketplaceType}
              />
            }
          />

          <InfoRow
            label="Статус на площадке"
            value={<MarketplaceOrderStatusChip value={order.marketplaceOrder} />}
          />

          {order.marketplaceOrder.trackingNumber && (
            <InfoRow
              label="Трек-номер"
              value={
                <Box component="span" sx={{fontFamily: "monospace"}}>
                  {order.marketplaceOrder.trackingNumber}
                </Box>
              }
            />
          )}

          <InfoRow
            label="Статус сверен"
            value={formatDate(order.marketplaceOrder.statusSyncedAt)}
          />

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
          <Button size="small" startIcon={<EditIcon />} onClick={() => setEditing(true)}>
            Редактировать
          </Button>
        </Box>
      )}
    </Stack>
  );
}

export default OrderMetaSection;
