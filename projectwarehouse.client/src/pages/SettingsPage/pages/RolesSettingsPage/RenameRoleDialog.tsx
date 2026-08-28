import {useState} from "react";
import {Button, Dialog, DialogActions, DialogContent, DialogTitle, TextField} from "@mui/material";
import {useBackClosable} from "@/hooks/useBackClosable";
import type {ModalComponentProps} from "@/contexts/Modal/ModalContext";

interface RenameRoleDialogProps extends ModalComponentProps<string> {
  initialName: string;
}

export default function RenameRoleDialog({open, onClose, initialName}: RenameRoleDialogProps) {
  const [name, setName] = useState(initialName);

  function handleSubmit() {
    const trimmed = name.trim();
    if (trimmed) onClose(trimmed);
  }

  useBackClosable(open, () => onClose(null));

  return (
    <Dialog open={open} onClose={() => onClose(null)} maxWidth="xs" fullWidth>
      <DialogTitle>Переименовать роль</DialogTitle>
      <DialogContent>
        <TextField
          autoFocus
          fullWidth
          label="Название роли"
          value={name}
          onChange={(e) => setName(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter") handleSubmit();
          }}
          sx={{mt: 1}}
        />
      </DialogContent>
      <DialogActions>
        <Button onClick={() => onClose(null)}>Отмена</Button>
        <Button variant="contained" disabled={!name.trim()} onClick={handleSubmit}>
          Сохранить
        </Button>
      </DialogActions>
    </Dialog>
  );
}
