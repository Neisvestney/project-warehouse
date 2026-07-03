import {type ReactNode, useState} from "react";
import {Button, Stack, TextField, Typography} from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import CheckIcon from "@mui/icons-material/Check";
import CloseIcon from "@mui/icons-material/Close";
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {ordersGetByIdQueryKey, ordersUpdateMutation} from "@/api/@tanstack/react-query.gen";
import type {OrderDetailsDto} from "@/api/types.gen";
import {format} from "date-fns";
import {ru} from "date-fns/locale";

function formatDate(iso: string | null | undefined): string {
  if (!iso) return "—";
  try {
    return format(new Date(iso), "d MMM yyyy, HH:mm", {locale: ru});
  } catch {
    return iso;
  }
}

function MetaRow({label, children}: {label: string; children: ReactNode}) {
  return (
    <Stack direction="row" spacing={1} sx={{alignItems: "flex-start", minHeight: 32}}>
      <Typography color="text.secondary" sx={{width: 160, flexShrink: 0, pt: 0.25}}>
        {label}
      </Typography>
      <Stack sx={{flex: 1}}>{children}</Stack>
    </Stack>
  );
}

interface InlineEditProps {
  value: string;
  onSave: (value: string) => void;
  multiline?: boolean;
  type?: string;
}

function InlineEdit({value, onSave, multiline, type = "text"}: InlineEditProps) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(value);

  if (!editing) {
    return (
      <Stack direction="row" spacing={0.5} sx={{alignItems: "center"}}>
        <Typography variant="body2">{value || "—"}</Typography>
        <Button
          size="small"
          sx={{minWidth: 0, p: 0.25}}
          onClick={() => {
            setDraft(value);
            setEditing(true);
          }}
        >
          <EditIcon sx={{fontSize: 14}} />
        </Button>
      </Stack>
    );
  }

  return (
    <Stack direction="row" spacing={0.5} sx={{alignItems: "flex-start"}}>
      <TextField
        size="small"
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        multiline={multiline}
        type={type}
        autoFocus
        sx={{flex: 1}}
      />
      <Button
        size="small"
        sx={{minWidth: 0, p: 0.25, mt: 0.5}}
        onClick={() => {
          onSave(draft);
          setEditing(false);
        }}
      >
        <CheckIcon sx={{fontSize: 14}} color="success" />
      </Button>
      <Button size="small" sx={{minWidth: 0, p: 0.25, mt: 0.5}} onClick={() => setEditing(false)}>
        <CloseIcon sx={{fontSize: 14}} />
      </Button>
    </Stack>
  );
}

interface OrderMetaSectionProps {
  order: OrderDetailsDto;
  canEdit: boolean;
}

function OrderMetaSection({order, canEdit}: OrderMetaSectionProps) {
  const queryClient = useQueryClient();

  const mutation = useMutation({
    ...ordersUpdateMutation(),
    onSuccess: () => {
      queryClient.invalidateQueries({queryKey: ordersGetByIdQueryKey({path: {id: order.id}})});
    },
  });

  function handleSaveNotes(notes: string) {
    mutation.mutate({
      path: {id: order.id},
      body: {notes: notes.trim() || null, plannedShipmentAt: order.plannedShipmentAt ?? null},
    });
  }

  function handleSavePlanned(planned: string) {
    mutation.mutate({
      path: {id: order.id},
      body: {notes: order.notes ?? null, plannedShipmentAt: planned || null},
    });
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
        {canEdit ? (
          <InlineEdit
            value={
              order.plannedShipmentAt
                ? new Date(order.plannedShipmentAt).toISOString().slice(0, 16)
                : ""
            }
            type="datetime-local"
            onSave={handleSavePlanned}
          />
        ) : (
          <Typography variant="body2">{formatDate(order.plannedShipmentAt)}</Typography>
        )}
      </MetaRow>

      <MetaRow label="Заметки">
        {canEdit ? (
          <InlineEdit value={order.notes ?? ""} multiline onSave={handleSaveNotes} />
        ) : (
          <Typography variant="body2">{order.notes || "—"}</Typography>
        )}
      </MetaRow>

      {order.marketplaceOrderId && (
        <MetaRow label="ID маркетплейса">
          <Typography variant="body2">{order.marketplaceOrderId}</Typography>
        </MetaRow>
      )}
    </Stack>
  );
}

export default OrderMetaSection;
