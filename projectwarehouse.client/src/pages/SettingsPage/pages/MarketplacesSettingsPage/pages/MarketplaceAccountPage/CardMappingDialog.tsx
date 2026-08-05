import {useEffect, useState} from "react";
import {
  Alert,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  Typography,
} from "@mui/material";
import {useMutation} from "@tanstack/react-query";
import {marketplacesSetCardMappingMutation} from "@/api/@tanstack/react-query.gen";
import {extractErrorMessage} from "@/utils/errorUtils";
import CatalogItemsSelect from "@/components/CatalogItemsSelect";
import CardImage from "../../components/CardImage";
import type {CatalogItemType, MarketplaceCardDto} from "@/api/types.gen";

// ProductGroup — виртуальная группа, компонентом заказа быть не может
const MAPPABLE_TYPES: CatalogItemType[] = ["standard", "unit", "bundle", "variation"];

interface CardMappingDialogProps {
  card: MarketplaceCardDto | null;
  onClose: () => void;
  onSaved: () => Promise<void> | void;
}

function CardMappingDialog({card, onClose, onSaved}: CardMappingDialogProps) {
  const [catalogItemId, setCatalogItemId] = useState<string | null>(null);

  // Сброс на текущую привязку при смене карточки — правка состояния в рендере, а не эффектом
  const [shownCardId, setShownCardId] = useState<string | null>(null);
  if (card && card.id !== shownCardId) {
    setShownCardId(card.id);
    setCatalogItemId(card.catalogItemId ?? null);
  }

  const mutation = useMutation({
    ...marketplacesSetCardMappingMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: async () => {
      await onSaved();
      onClose();
    },
  });

  const {reset} = mutation;
  useEffect(() => {
    if (!card) reset();
  }, [card, reset]);

  const save = (value: string | null) => {
    if (!card) return;
    mutation.mutate({path: {id: card.id}, body: {catalogItemId: value}});
  };

  return (
    <Dialog
      open={!!card}
      onClose={mutation.isPending ? undefined : onClose}
      maxWidth="sm"
      fullWidth
    >
      <DialogTitle>Привязка карточки</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{pt: 1}}>
          {card && (
            <Stack direction="row" spacing={2} sx={{alignItems: "center"}}>
              <CardImage src={card.primaryImageUrl} name={card.name} size={72} />
              <Stack>
                <Typography>{card.name}</Typography>
                <Typography variant="caption" color="text.secondary">
                  Артикул {card.offerId}
                  {card.sku ? ` · SKU ${card.sku}` : ""}
                </Typography>
              </Stack>
            </Stack>
          )}
          <CatalogItemsSelect
            value={catalogItemId}
            onChange={setCatalogItemId}
            types={MAPPABLE_TYPES}
            disabled={mutation.isPending}
            fullWidth
          />
          {mutation.isError && (
            <Alert severity="error">{extractErrorMessage(mutation.error)}</Alert>
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={mutation.isPending}>
          Отмена
        </Button>
        {card?.catalogItemId && (
          <Button color="error" onClick={() => save(null)} disabled={mutation.isPending}>
            Снять привязку
          </Button>
        )}
        <Button
          variant="contained"
          onClick={() => save(catalogItemId)}
          disabled={mutation.isPending || !catalogItemId}
        >
          {mutation.isPending ? <CircularProgress size={20} color="inherit" /> : "Сохранить"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

export default CardMappingDialog;
