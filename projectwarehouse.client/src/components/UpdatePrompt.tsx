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
import {useFadeOnHover} from "@/hooks/useFadeOnHover.ts";

const FLOAT_LEFT = 16;
const FADE_MS = 150;

const HIDE_ON_PRINT = {"@media print": {display: "none"}} as const;

export interface UpdatePromptProps {}

function UpdatePrompt({}: UpdatePromptProps) {
  const {installing, needRefresh, updateServiceWorker} = useContext(ServiceWorkerContext);
  const [dismissed, setDismissed] = useState(false);
  const floatTop = useFloatTop();
  const {ref: floatRef, faded, onPointerEnter} = useFadeOnHover<HTMLDivElement>();

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
    ...HIDE_ON_PRINT,
  } as const;

  // Only the progress plaque steps aside: it carries nothing to click, while the waiting one exists
  // to be clicked and going transparent to the pointer would put its button out of reach.
  const fadingFloatProps = {
    ref: floatRef,
    onPointerEnter,
    sx: {
      ...floatSx,
      opacity: faded ? 0 : 1,
      pointerEvents: faded ? "none" : "auto",
      transition: `opacity ${FADE_MS}ms`,
    },
  } as const;

  if (installing) {
    return (
      <Paper {...fadingFloatProps}>
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
      <Dialog open onClose={() => {}} sx={HIDE_ON_PRINT}>
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
