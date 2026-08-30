import {useCallback, useEffect, useState} from "react";
import {
  Alert,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  Stack,
  Switch,
  Typography,
} from "@mui/material";
import {useBackClosable} from "@/hooks/useBackClosable";
import {useMutation} from "@tanstack/react-query";
import {marketplacesSetCardMappingMutation} from "@/api/@tanstack/react-query.gen";
import {extractErrorMessage} from "@/utils/errorUtils";
import {useEditLock} from "@/hooks/useEditLock";
import CatalogItemsSelect from "@/components/CatalogItemsSelect";
import EditLockBanner from "@/components/EditLockBanner";
import EntityViewers from "@/components/EntityViewers";
import StaleDataBanner from "@/components/StaleDataBanner";
import CardImage from "../../components/CardImage";
import type {CatalogItemType, MarketplaceCardDto} from "@/api/types.gen";
import {useRetainedValue} from "@/hooks/useRetainedValue";

// ProductGroup — виртуальная группа, компонентом заказа быть не может
const MAPPABLE_TYPES: CatalogItemType[] = ["standard", "unit", "bundle", "variation"];

interface CardMappingDialogProps {
  card: MarketplaceCardDto | null;
  onClose: () => void;
  onSaved: () => Promise<void> | void;
  /** `dataUpdatedAt` of the card list this dialog was opened from. */
  dataUpdatedAt: number;
}

function CardMappingDialog({card, onClose, onSaved, dataUpdatedAt}: CardMappingDialogProps) {
  const [catalogItemId, setCatalogItemId] = useState<string | null>(null);
  const [isMarkedArchived, setIsMarkedArchived] = useState(false);
  const [shownCard, releaseShownCard] = useRetainedValue(card);

  // Сброс на текущую привязку при смене карточки — правка состояния в рендере, а не эффектом
  const [shownCardId, setShownCardId] = useState<string | null>(null);
  if (card && card.id !== shownCardId) {
    setShownCardId(card.id);
    setCatalogItemId(card.catalogItemId ?? null);
    setIsMarkedArchived(card.isMarkedArchived);
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
    if (!shownCard) reset();
  }, [shownCard, reset]);

  const save = (value: string | null) => {
    if (!card) return;
    mutation.mutate({path: {id: card.id}, body: {catalogItemId: value, isMarkedArchived}});
  };

  const refreshCard = useCallback(() => void onSaved(), [onSaved]);

  const isDirty =
    !!card &&
    (catalogItemId !== (card.catalogItemId ?? null) || isMarkedArchived !== card.isMarkedArchived);

  // The dialog being open is the edit mode; a picked-but-unsaved mapping is what must not be
  // overwritten silently.
  const lock = useEditLock("marketplaceCard", card?.id, {
    isDirty,
    dataUpdatedAt,
    onRefresh: refreshCard,
    enabled: !!card,
  });

  useBackClosable(!!card && !mutation.isPending, onClose);

  return (
    <Dialog
      open={!!card}
      onClose={mutation.isPending ? undefined : onClose}
      maxWidth="sm"
      fullWidth
      slotProps={{
        transition: {onExited: releaseShownCard},
        paper: {sx: {pointerEvents: card ? undefined : "none"}},
      }}
    >
      <DialogTitle>
        <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
          <span>Привязка карточки</span>
          <EntityViewers entityType="marketplaceCard" entityId={shownCard?.id} />
        </Stack>
      </DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{pt: 1}}>
          <EditLockBanner heldBy={lock.heldBy} />
          <StaleDataBanner
            isStale={!lock.heldBy && lock.isStale}
            staleBy={lock.staleBy}
            onRefresh={lock.refresh}
            onDismiss={lock.dismissStale}
          />

          {shownCard && (
            <Stack direction="row" spacing={2} sx={{alignItems: "center"}}>
              <CardImage src={shownCard.primaryImageUrl} name={shownCard.name} size={72} />
              <Stack>
                <Typography>{shownCard.name}</Typography>
                <Typography variant="caption" color="text.secondary">
                  Артикул {shownCard.offerId}
                  {shownCard.sku ? ` · SKU ${shownCard.sku}` : ""}
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
          <FormControlLabel
            control={
              <Switch
                checked={isMarkedArchived}
                onChange={(e) => setIsMarkedArchived(e.target.checked)}
                disabled={mutation.isPending}
              />
            }
            label="Не используется на складе"
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
        {shownCard?.catalogItemId && (
          <Button color="error" onClick={() => save(null)} disabled={mutation.isPending}>
            Снять привязку
          </Button>
        )}
        <Button
          variant="contained"
          onClick={() => save(catalogItemId)}
          disabled={mutation.isPending || !isDirty}
        >
          {mutation.isPending ? <CircularProgress size={20} color="inherit" /> : "Сохранить"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

export default CardMappingDialog;
