import {useState} from "react";
import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  TextField,
} from "@mui/material";
import {useBackClosable} from "@/hooks/useBackClosable";
import CatalogItemsSelect from "@/components/CatalogItemsSelect";
import {ClampedIntegerField} from "@/components/form/ClampedIntegerField";
import type {CatalogItemSelectDto} from "@/api/types.gen";

interface AddedRow {
  kind: "standard" | "unit";
  catalogItemId: string;
  catalogItemName: string;
  counted: number;
  inventoryNumber?: string;
}

interface StocktakeAddItemModalProps {
  open: boolean;
  onClose: () => void;
  onAdd: (row: AddedRow) => void;
}

function StocktakeAddItemModal({open, onClose, onAdd}: StocktakeAddItemModalProps) {
  const [catalogItemId, setCatalogItemId] = useState<string | null>(null);
  const [dto, setDto] = useState<CatalogItemSelectDto | null>(null);
  const [count, setCount] = useState(1);
  const [inventoryNumber, setInventoryNumber] = useState("");

  const isUnit = dto?.type === "unit";
  const canSubmit = !!catalogItemId && !!dto && (!isUnit || inventoryNumber.trim().length > 0);

  const reset = () => {
    setCatalogItemId(null);
    setDto(null);
    setCount(1);
    setInventoryNumber("");
  };

  const handleClose = () => {
    reset();
    onClose();
  };

  const handleSubmit = () => {
    if (!catalogItemId || !dto) return;
    onAdd({
      kind: isUnit ? "unit" : "standard",
      catalogItemId,
      catalogItemName: dto.name,
      counted: isUnit ? 1 : count,
      inventoryNumber: isUnit ? inventoryNumber.trim() : undefined,
    });
    reset();
  };

  useBackClosable(open, handleClose);

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>Добавить товар</DialogTitle>
      <DialogContent>
        <Stack spacing={2.5} sx={{pt: 1}}>
          <CatalogItemsSelect
            value={catalogItemId}
            onChange={setCatalogItemId}
            onDtoChange={setDto}
            types={["standard", "unit"]}
            fullWidth
          />
          {isUnit ? (
            <TextField
              label="Инвентарный номер"
              value={inventoryNumber}
              onChange={(e) => setInventoryNumber(e.target.value)}
              size="small"
              fullWidth
              required
            />
          ) : (
            <ClampedIntegerField
              label="Количество"
              value={count}
              min={1}
              onCommit={setCount}
              size="small"
              fullWidth
            />
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={handleClose}>Отмена</Button>
        <Button variant="contained" disabled={!canSubmit} onClick={handleSubmit}>
          Добавить
        </Button>
      </DialogActions>
    </Dialog>
  );
}

export default StocktakeAddItemModal;
