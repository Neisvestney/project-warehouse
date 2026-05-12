import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
} from "@mui/material";
import type {ConfirmOptions, ModalComponentProps} from "@/contexts/Modal/ModalContext";

type ConfirmModalProps = ModalComponentProps<boolean> & ConfirmOptions;

const severityColor = {
  error: "error",
  warning: "warning",
  info: "info",
} as const;

export default function ConfirmModal({
  open,
  onClose,
  title,
  message,
  severity = "warning",
  confirmText = "Подтвердить",
  cancelText = "Отмена",
}: ConfirmModalProps) {
  const color = severityColor[severity];

  return (
    <Dialog open={open} onClose={() => onClose(false)}>
      <DialogTitle sx={{color: `${color}.main`}}>{title}</DialogTitle>
      <DialogContent>
        <DialogContentText>{message}</DialogContentText>
      </DialogContent>
      <DialogActions>
        <Button onClick={() => onClose(false)}>{cancelText}</Button>
        <Button onClick={() => onClose(true)} color={color} variant="contained" autoFocus>
          {confirmText}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
