import {
  Button,
  Card,
  CardActionArea,
  CardActions,
  CardContent,
  Stack,
  Typography,
  Box,
  CircularProgress,
  Fab,
  useMediaQuery,
} from "@mui/material";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import OfflinePinIcon from "@mui/icons-material/OfflinePin";
import CameraAltIcon from "@mui/icons-material/CameraAlt";
import React, {useContext} from "react";
import {Link, useNavigate} from "react-router";
import ServiceWorkerContext from "@/contexts/ServiceWorker/ServiceWorkerContext.ts";
import InstallPrompt from "@/components/InstallPrompt.tsx";
import {useQuery} from "@tanstack/react-query";
import {commonContentGetHomePageContentOptions} from "@/api/@tanstack/react-query.gen.ts";
import {resolveEntity} from "@/utils/appEntityUtils.tsx";
import AppEvents from "@/components/AppEvents.tsx";
import theme from "@/theme.ts";
import {useGlobalSearch} from "@/contexts/GlobalSearch/GlobalSearchContext.ts";
import {getScannedEntityLink} from "@/components/GlobalSearch/scannedEntityLink.ts";
import {useHardwareScanner} from "@/hooks/useHardwareScanner.ts";
import {isCameraApiSupported} from "@/utils/camera/cameraUtils.ts";
import {extractMarkGtin} from "@/utils/gtinUtils";

export interface HomePageProps {}

function HomePage({}: HomePageProps) {
  const swContext = useContext(ServiceWorkerContext);
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  const navigate = useNavigate();
  const {isOpen: searchOpen, openSearch, closeSearch} = useGlobalSearch();

  const {data, isError} = useQuery({
    ...commonContentGetHomePageContentOptions(),
    meta: {suppressGlobalError: true},
  });

  useHardwareScanner(({barcode}) => {
    const link = getScannedEntityLink(barcode);
    if (link) {
      // The open search modal holds a history entry of its own — take its place instead of stacking above it
      navigate(link, {replace: searchOpen});
      closeSearch();
      return;
    }
    openSearch({query: extractMarkGtin(barcode) ?? barcode.trim()});
  });

  const showCameraFab = isMobile && isCameraApiSupported();

  return (
    // Bottom room so the fixed FAB does not cover the last card
    <Stack spacing={2} sx={{pb: showCameraFab ? 10 : 0}}>
      {!isMobile && <AppEvents />}
      <Box
        sx={{
          width: "100%",
          display: "grid",
          gap: 2,
          gridTemplateColumns: {md: "repeat(auto-fill, minmax(200px, 1fr))", sx: "1fr"},
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
        {!data && !isError && <HomeCard loading />}
        {data?.map(resolveEntity).map((x) => (
          <HomeCard title={x.name ?? x.typeName} link={x.link} linkText={"Перейти"} icon={x.icon}>
            {x.renderAdditionalCardContent?.(x)}
          </HomeCard>
        ))}
      </Box>
      {showCameraFab && (
        <Fab
          color="primary"
          aria-label="Сканировать камерой"
          onClick={() => openSearch({camera: true})}
          sx={{position: "fixed", bottom: 24, right: 24}}
        >
          <CameraAltIcon />
        </Fab>
      )}
    </Stack>
  );
}

export default HomePage;

function HomeCard({
  title,
  link,
  linkText,
  icon,
  loading,
  children,
}: {
  title?: string;
  link?: string;
  linkText?: string;
  icon?: React.ReactNode;
  loading?: boolean;
  children?: React.ReactNode;
}) {
  return (
    <Card>
      {!loading ? (
        <CardActionArea
          sx={{
            height: "100%",
            display: "flex",
            flexDirection: "column",
            alignItems: "unset",
            justifyContent: "space-between",
          }}
          component={Link}
          to={link ?? "#"}
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
            {children}
          </CardContent>
          <CardActions sx={{justifyContent: "end"}}>
            <Button component={"span"} size="small" endIcon={<ArrowForwardIcon />}>
              {linkText}
            </Button>
          </CardActions>
        </CardActionArea>
      ) : (
        <Stack
          sx={{width: "100%", height: "100%", justifyContent: "center", alignItems: "center", p: 5}}
        >
          <CircularProgress size={32} />
        </Stack>
      )}
    </Card>
  );
}
