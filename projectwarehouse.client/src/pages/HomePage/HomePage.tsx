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
} from "@mui/material";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import OfflinePinIcon from "@mui/icons-material/OfflinePin";
import React, {useContext} from "react";
import {Link} from "react-router";
import ServiceWorkerContext from "@/contexts/ServiceWorker/ServiceWorkerContext.ts";
import InstallPrompt from "@/components/InstallPrompt.tsx";
import {useQuery} from "@tanstack/react-query";
import {homePageContentGetHomePageContentOptions} from "@/api/@tanstack/react-query.gen.ts";
import {resolveEntity} from "@/utils/appEntityUtils.tsx";

export interface HomePageProps {}

function HomePage({}: HomePageProps) {
  const swContext = useContext(ServiceWorkerContext);

  const {data, isError} = useQuery({
    ...homePageContentGetHomePageContentOptions(),
    meta: {suppressGlobalError: true},
  });

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
      {!data && !isError && <HomeCard loading />}
      {data?.map(resolveEntity).map((x) => (
        <HomeCard title={x.name ?? x.typeName} link={x.link} linkText={"Перейти"} icon={x.icon} />
      ))}
    </Box>
  );
}

export default HomePage;

function HomeCard({
  title,
  link,
  linkText,
  icon,
  loading,
}: {
  title?: string;
  link?: string;
  linkText?: string;
  icon?: React.ReactNode;
  loading?: boolean;
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
