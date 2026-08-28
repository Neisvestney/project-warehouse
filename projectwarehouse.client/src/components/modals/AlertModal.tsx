import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
} from "@mui/material";
import {useBackClosable} from "@/hooks/useBackClosable";
import type {AlertOptions, ModalComponentProps} from "@/contexts/Modal/ModalContext";

type AlertModalProps = ModalComponentProps<void> & AlertOptions;

const severityColor = {
  error: "error",
  warning: "warning",
  info: "info",
  success: "success",
} as const;

export default function AlertModal({
  open,
  onClose,
  title,
  message,
  severity = "error",
  confirmText = "OK",
}: AlertModalProps) {
  const color = severityColor[severity];

  useBackClosable(open, () => onClose(null));

  return (
    <Dialog open={open} onClose={() => onClose(null)}>
      <DialogTitle sx={{color: `${color}.main`}}>{title}</DialogTitle>
      <DialogContent>
        <DialogContentText>{message}</DialogContentText>
      </DialogContent>
      <DialogActions>
        <Button onClick={() => onClose(null)} color={color} variant="contained" autoFocus>
          {confirmText}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
