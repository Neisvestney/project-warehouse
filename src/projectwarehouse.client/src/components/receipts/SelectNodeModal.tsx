import {Dialog, DialogContent, DialogTitle, IconButton, Stack, Typography} from "@mui/material";
import {useBackClosable} from "@/hooks/useBackClosable";
import CloseIcon from "@mui/icons-material/Close";
import StorageNodePickerContent from "@/components/shared/StorageNodePickerContent";
import type {SelectedNode} from "@/components/shared/nodePathUtils";

export type {SelectedNode};

interface SelectNodeModalProps {
  open: boolean;
  onClose: () => void;
  warehouseId: string;
  onSelect: (node: SelectedNode) => void;
  catalogItemId?: string;
}

function SelectNodeModal({
  open,
  onClose,
  warehouseId,
  onSelect,
  catalogItemId,
}: SelectNodeModalProps) {
  useBackClosable(open, onClose);

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{pb: 0}}>
        <Stack direction="row" sx={{alignItems: "center"}}>
          <Typography variant="h6" sx={{flexGrow: 1}}>
            Выбор ячейки
          </Typography>
          <IconButton onClick={onClose} size="small">
            <CloseIcon />
          </IconButton>
        </Stack>
      </DialogTitle>
      <DialogContent sx={{p: 0}}>
        <StorageNodePickerContent
          warehouseId={warehouseId}
          onSelect={onSelect}
          open={open}
          catalogItemId={catalogItemId}
        />
      </DialogContent>
    </Dialog>
  );
}

export default SelectNodeModal;
