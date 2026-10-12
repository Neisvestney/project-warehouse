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
import {EMPTY_TAG_FILTER, type TagFilter} from "@/features/tags";

/** Matches the `take` cap of `GET /api/catalog/for-select`. */
const MAX_ITEMS = 200;

type AddItemsByTagDialogProps = {
  open: boolean;
  onClose: () => void;
  onAdd: (items: CatalogItemSelectDto[]) => void;
  types?: CatalogItemType[];
};

function AddItemsByTagDialog({open, onClose, onAdd, types}: AddItemsByTagDialogProps) {
  const [tagFilter, setTagFilter] = useState<TagFilter>(EMPTY_TAG_FILTER);
  // Exclusions only narrow the picked tags: on their own they would pour in the whole catalog
  const hasIncluded = tagFilter.include.length > 0;

  const itemsQuery = useQuery({
    ...catalogGetForSelectOptions({
      query: {
        tagIds: tagFilter.include,
        excludedTagIds: tagFilter.exclude,
        types,
        take: MAX_ITEMS,
      },
    }),
    enabled: open && hasIncluded,
  });

  const foundItems = hasIncluded ? (itemsQuery.data ?? []) : [];

  const handleClose = () => {
    setTagFilter(EMPTY_TAG_FILTER);
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
          <CatalogTagsFilter value={tagFilter} onChange={setTagFilter} autoFocus />

          <Typography variant="body2" color="text.secondary">
            {!hasIncluded
              ? "Выберите один или несколько тегов — будут добавлены все позиции с любым из них, кроме позиций с исключёнными тегами."
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
