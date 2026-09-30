import {useState} from "react";
import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  Typography,
} from "@mui/material";
import {useBackClosable} from "@/hooks/useBackClosable";
import {useQuery} from "@tanstack/react-query";
import {catalogGetForSelectOptions} from "@/api/@tanstack/react-query.gen";
import type {CatalogItemSelectDto, CatalogItemType} from "@/api/types.gen";
import CatalogTagsFilter from "@/components/catalog/CatalogTagsFilter";

/** Matches the `take` cap of `GET /api/catalog/for-select`. */
const MAX_ITEMS = 200;

type AddItemsByTagDialogProps = {
  open: boolean;
  onClose: () => void;
  onAdd: (items: CatalogItemSelectDto[]) => void;
  types?: CatalogItemType[];
};

function AddItemsByTagDialog({open, onClose, onAdd, types}: AddItemsByTagDialogProps) {
  const [tagIds, setTagIds] = useState<string[]>([]);

  const itemsQuery = useQuery({
    ...catalogGetForSelectOptions({query: {tagIds, types, take: MAX_ITEMS}}),
    enabled: open && tagIds.length > 0,
  });

  const foundItems = tagIds.length > 0 ? (itemsQuery.data ?? []) : [];

  const handleClose = () => {
    setTagIds([]);
    onClose();
  };

  const handleAdd = () => {
    onAdd(foundItems);
    handleClose();
  };

  useBackClosable(open, handleClose);

  return (
    <Dialog open={open} onClose={handleClose} fullWidth maxWidth="sm">
      <DialogTitle>Добавить позиции по тегу</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{pt: 1}}>
          <CatalogTagsFilter value={tagIds} onChange={setTagIds} autoFocus />

          <Typography variant="body2" color="text.secondary">
            {tagIds.length === 0
              ? "Выберите один или несколько тегов — будут добавлены все позиции с любым из них."
              : itemsQuery.isFetching
                ? "Поиск позиций…"
                : `Найдено позиций: ${foundItems.length}`}
          </Typography>

          {foundItems.length === MAX_ITEMS && (
            <Alert severity="warning">
              Показаны первые {MAX_ITEMS} позиций — сузьте набор тегов, часть может не попасть в
              выборку.
            </Alert>
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button color="inherit" onClick={handleClose}>
          Отмена
        </Button>
        <Button
          variant="contained"
          disabled={foundItems.length === 0 || itemsQuery.isFetching}
          onClick={handleAdd}
        >
          Добавить
        </Button>
      </DialogActions>
    </Dialog>
  );
}

export default AddItemsByTagDialog;
