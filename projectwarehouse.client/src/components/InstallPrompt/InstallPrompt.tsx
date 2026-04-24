import React from "react";
import {useInstallPrompt} from "../../utils/useInstallPrompt.ts";
import {
  Button,
  Card,
  CardActionArea,
  CardActions,
  CardContent,
  Stack,
  Typography,
} from "@mui/material";
import DownloadIcon from "@mui/icons-material/Download";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";

export interface InstallPromptProps {}

function InstallPrompt({}: InstallPromptProps) {
  const {canInstall, triggerInstall} = useInstallPrompt();

  if (!canInstall && !import.meta.env.DEV) return null;

  return (
    <Card>
      <CardActionArea
        sx={{
          height: "100%",
          display: "flex",
          flexDirection: "column",
          alignItems: "unset",
          justifyContent: "space-between",
        }}
        onClick={triggerInstall}
      >
        <CardContent>
          <Stack
            direction="row"
            spacing={1}
            sx={{
              alignItems: "center",
            }}
          >
            <DownloadIcon />
            <Typography gutterBottom variant="h5" component="div">
              Установить
            </Typography>
          </Stack>
          <Typography gutterBottom variant="body1" component="div">
            Добавить приложение на главный экран
          </Typography>
        </CardContent>
        <CardActions sx={{justifyContent: "end"}}>
          <Button component={"span"} size="small" endIcon={<ArrowForwardIcon />}>
            Установить
          </Button>
        </CardActions>
      </CardActionArea>
    </Card>
  );
}

export default InstallPrompt;
