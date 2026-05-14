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
import WarehouseIcon from "@mui/icons-material/Warehouse";
import React, {useContext} from "react";
import {Link} from "react-router";
import ServiceWorkerContext from "@/contexts/ServiceWorker/ServiceWorkerContext.ts";
import InstallPrompt from "@/components/InstallPrompt.tsx";
import {useHasPermission} from "@/hooks/usePermission.ts";

export interface HomePageProps {}

function HomePage({}: HomePageProps) {
  const swContext = useContext(ServiceWorkerContext);

  const canUserViewWarehouses = useHasPermission("warehouses.view");

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
      {canUserViewWarehouses && (
        <HomeCard
          title={"Склады"}
          link={"/warehouses"}
          linkText={"Посмотреть список"}
          icon={<WarehouseIcon />}
        />
      )}
      <HomeCard
        title={"Сканер"}
        link={"/scanner"}
        linkText={"Начать сканировать"}
        icon={<QrCodeScannerIcon />}
      />
    </Box>
  );
}

export default HomePage;

function HomeCard({
  title,
  link,
  linkText,
  icon,
}: {
  title: string;
  link: string;
  linkText: string;
  icon: React.ReactNode;
}) {
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
        component={Link}
        to={link}
      >
        <CardContent>
          <Stack
            direction="row"
            spacing={1}
            sx={{
              alignItems: "center",
            }}
          >
            {icon}
            <Typography gutterBottom variant="h5" component="div">
              {title}
            </Typography>
          </Stack>
        </CardContent>
        <CardActions sx={{justifyContent: "end"}}>
          <Button component={"span"} size="small" endIcon={<ArrowForwardIcon />}>
            {linkText}
          </Button>
        </CardActions>
      </CardActionArea>
    </Card>
  );
}
