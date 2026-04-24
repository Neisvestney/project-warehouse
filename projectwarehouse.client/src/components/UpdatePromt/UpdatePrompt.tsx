import React, {useContext, useState} from "react";
import ServiceWorkerContext from "../../contexts/ServiceWorkerContext.ts";
import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
} from "@mui/material";

export interface UpdatePromptProps {}

function UpdatePrompt({}: UpdatePromptProps) {
  const {needRefresh, updateServiceWorker} = useContext(ServiceWorkerContext);

  const [open, setOpen] = useState(needRefresh);

  const handleClose = () => {
    setOpen(false);
  };

  const handleAgree = () => {
    updateServiceWorker(true);
  };

  const [prevNeedRefresh, setPrevNeedRefresh] = useState(needRefresh);
  if (needRefresh !== prevNeedRefresh) {
    setPrevNeedRefresh(needRefresh);
    setOpen(true);
  }

  return (
    <Dialog
      open={open}
      onClose={handleClose}
      aria-labelledby="alert-dialog-title"
      aria-describedby="alert-dialog-description"
      role="alertdialog"
    >
      <DialogTitle id="alert-dialog-title">{"Доступно обновление приложения"}</DialogTitle>
      <DialogContent>
        <DialogContentText id="alert-dialog-description">
          Перезагрузите приложение для применения обновления
        </DialogContentText>
      </DialogContent>
      <DialogActions>
        <Button onClick={handleClose}>Отложить</Button>
        <Button onClick={handleAgree} autoFocus>
          Обновить
        </Button>
      </DialogActions>
    </Dialog>
  );
}

export default UpdatePrompt;
