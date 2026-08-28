import React from "react";
import type {ButtonProps, DialogProps} from "@mui/material";
import {useBackClosable} from "@/hooks/useBackClosable";
import {
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
} from "@mui/material";

interface ConfirmDialogProps {
  open: boolean;
  onClose: () => void;
  title: string;
  children?: React.ReactNode;
  onConfirm: () => void;
  isPending?: boolean;
  confirmText?: string;
  confirmColor?: ButtonProps["color"];
  maxWidth?: DialogProps["maxWidth"];
}

function ConfirmDialog({
  open,
  onClose,
  title,
  children,
  onConfirm,
  isPending = false,
  confirmText = "Подтвердить",
  confirmColor = "primary",
  maxWidth = "xs",
}: ConfirmDialogProps) {
  useBackClosable(open && !isPending, onClose);

  return (
    <Dialog open={open} onClose={isPending ? undefined : onClose} maxWidth={maxWidth} fullWidth>
      <DialogTitle>{title}</DialogTitle>
      {children && <DialogContent>{children}</DialogContent>}
      <DialogActions>
        <Button onClick={onClose} disabled={isPending}>
          Отмена
        </Button>
        <Button color={confirmColor} variant="contained" onClick={onConfirm} disabled={isPending}>
          {isPending ? <CircularProgress size={20} color="inherit" /> : confirmText}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

export default ConfirmDialog;
