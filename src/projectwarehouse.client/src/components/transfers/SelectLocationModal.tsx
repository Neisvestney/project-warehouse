import {useState} from "react";
import {
  Box,
  Dialog,
  DialogContent,
  DialogTitle,
  Divider,
  IconButton,
  Stack,
  Typography,
} from "@mui/material";
import {useBackClosable} from "@/hooks/useBackClosable";
import CloseIcon from "@mui/icons-material/Close";
import WarehousesSelect from "@/components/WarehousesSelect";
import StorageNodePickerContent from "@/components/shared/StorageNodePickerContent";
import type {SelectedNode} from "@/components/shared/nodePathUtils";

export interface SelectedLocation {
  nodeId: string;
  nodePath: string[];
  warehouseId: string;
  warehouseName: string;
}

interface SelectLocationModalProps {
  open: boolean;
  onClose: () => void;
  onSelect: (location: SelectedLocation) => void;
}

function SelectLocationModal({open, onClose, onSelect}: SelectLocationModalProps) {
  const [warehouseId, setWarehouseId] = useState<string | null>(null);
  const [warehouseName, setWarehouseName] = useState<string>("");

  const handleNodeSelect = (node: SelectedNode) => {
    if (!warehouseId) return;
    onSelect({
      nodeId: node.nodeId,
      nodePath: node.nodePath,
      warehouseId,
      warehouseName,
    });
    onClose();
  };

  const handleClose = () => {
    onClose();
  };

  // Reset after the exit transition, otherwise the picker empties while the dialog is still fading.
  const handleExited = () => {
    setWarehouseId(null);
    setWarehouseName("");
  };

  useBackClosable(open, handleClose);

  return (
    <Dialog
      open={open}
      onClose={handleClose}
      maxWidth="sm"
      fullWidth
      slotProps={{
        transition: {onExited: handleExited},
        paper: {sx: {pointerEvents: open ? undefined : "none"}},
      }}
    >
      <DialogTitle sx={{pb: 0}}>
        <Stack direction="row" sx={{alignItems: "center"}}>
          <Typography variant="h6" sx={{flexGrow: 1}}>
            Выбор места хранения
          </Typography>
          <IconButton onClick={handleClose} size="small">
            <CloseIcon />
          </IconButton>
        </Stack>
      </DialogTitle>
      <DialogContent sx={{p: 0}}>
        <Box sx={{px: 2, pt: 2, pb: 1.5}}>
          <WarehousesSelect
            value={warehouseId}
            onChange={setWarehouseId}
            onDtoChange={(dto) => setWarehouseName(dto?.name ?? "")}
            textFieldProps={{label: "Склад"}}
            size="small"
            fullWidth
          />
        </Box>

        {warehouseId && (
          <>
            <Divider />
            <StorageNodePickerContent
              warehouseId={warehouseId}
              onSelect={handleNodeSelect}
              open={open}
            />
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}

export default SelectLocationModal;
