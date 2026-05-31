import React, {useContext, useState} from "react";
import ServiceWorkerContext from "@/contexts/ServiceWorker/ServiceWorkerContext.ts";
import {
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  LinearProgress,
  Paper,
  Typography,
} from "@mui/material";
import DownloadingIcon from "@mui/icons-material/Downloading";
import SystemUpdateAltIcon from "@mui/icons-material/SystemUpdateAlt";
import {useFloatTop} from "@/hooks/useFloatTop.ts";

const FLOAT_LEFT = 16;

export interface UpdatePromptProps {}

function UpdatePrompt({}: UpdatePromptProps) {
  const {installing, needRefresh, updateServiceWorker} = useContext(ServiceWorkerContext);
  const [dismissed, setDismissed] = useState(false);
  const floatTop = useFloatTop();

  const [prevNeedRefresh, setPrevNeedRefresh] = useState(needRefresh);
  if (needRefresh !== prevNeedRefresh) {
    setPrevNeedRefresh(needRefresh);
    if (needRefresh) setDismissed(false);
  }

  const floatSx = {
    position: "fixed",
    top: floatTop,
    left: FLOAT_LEFT,
    zIndex: 1050,
    p: 2,
    maxWidth: 280,
    display: "flex",
    flexDirection: "column",
    gap: 1,
    boxShadow: 4,
  } as const;

  if (installing) {
    return (
      <Paper sx={floatSx}>
        <Box sx={{display: "flex", alignItems: "center", gap: 1}}>
          <DownloadingIcon fontSize="small" color="primary" />
          <Typography variant="body2" sx={{fontWeight: 500}}>
            Установка обновления...
          </Typography>
        </Box>
        <LinearProgress />
      </Paper>
    );
  }

  if (needRefresh && !dismissed) {
    return (
      <Dialog open onClose={() => {}}>
        <DialogTitle>Доступно обновление приложения</DialogTitle>
        <DialogContent>
          <DialogContentText>Перезагрузите приложение для применения обновления</DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDismissed(true)}>Отложить</Button>
          <Button onClick={() => updateServiceWorker(true)} autoFocus variant="contained">
            Обновить
          </Button>
        </DialogActions>
      </Dialog>
    );
  }

  if (needRefresh && dismissed) {
    return (
      <Paper sx={floatSx}>
        <Box sx={{display: "flex", alignItems: "center", gap: 1}}>
          <SystemUpdateAltIcon fontSize="small" color="warning" />
          <Typography variant="body2" sx={{fontWeight: 500}}>
            Обновление ожидает перезапуска
          </Typography>
        </Box>
        <Button
          size="small"
          variant="outlined"
          color="warning"
          onClick={() => updateServiceWorker(true)}
          fullWidth
        >
          Перезагрузить
        </Button>
      </Paper>
    );
  }

  return null;
}

export default UpdatePrompt;
