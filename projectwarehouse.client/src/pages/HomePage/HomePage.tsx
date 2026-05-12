import {
  Button,
  Card,
  CardActionArea,
  CardActions,
  CardContent,
  Stack,
  Typography,
  Box,
} from "@mui/material";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import OfflinePinIcon from "@mui/icons-material/OfflinePin";
import QrCodeScannerIcon from "@mui/icons-material/QrCodeScanner";
import React, {useContext} from "react";
import {Link} from "react-router";
import ServiceWorkerContext from "@/contexts/ServiceWorker/ServiceWorkerContext.ts";
import InstallPrompt from "@/components/InstallPrompt.tsx";

export interface HomePageProps {}

function HomePage({}: HomePageProps) {
  const swContext = useContext(ServiceWorkerContext);

  return (
    <Box
      sx={{
        display: "grid",
        gap: 2,
        gridTemplateColumns: {md: "repeat(auto-fit, minmax(200px, 250px))", sx: "1fr"},
      }}
    >
      <InstallPrompt />
      {swContext.offlineReady ||
        (import.meta.env.DEV && (
          <Card>
            <CardContent>
              <Stack
                direction="row"
                spacing={1}
                sx={{
                  alignItems: "center",
                }}
              >
                <OfflinePinIcon />
                <Typography gutterBottom variant="h5" component="div">
                  Приложение
                </Typography>
              </Stack>
              <Typography gutterBottom variant="body1" component="div">
                Приложение готово к работе в оффлайн
              </Typography>
            </CardContent>
          </Card>
        ))}
      <Card>
        <CardActionArea
          sx={{
            height: "100%",
            display: "flex",
            flexDirection: "column",
            alignItems: "unset",
            justifyContent: "space-between",
          }}
          component={Link}
          to={"/scanner"}
        >
          <CardContent>
            <Stack
              direction="row"
              spacing={1}
              sx={{
                alignItems: "center",
              }}
            >
              <QrCodeScannerIcon />
              <Typography gutterBottom variant="h5" component="div">
                Сканер
              </Typography>
            </Stack>
          </CardContent>
          <CardActions sx={{justifyContent: "end"}}>
            <Button component={"span"} size="small" endIcon={<ArrowForwardIcon />}>
              Начать сканировать
            </Button>
          </CardActions>
        </CardActionArea>
      </Card>
    </Box>
  );
}

export default HomePage;
