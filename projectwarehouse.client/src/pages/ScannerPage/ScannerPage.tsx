import React, {useCallback, useState} from "react";
import {
  SnackbarContent,
  CardContent,
  css,
  Fab,
  Stack,
  styled,
  Typography,
  Paper,
  Card,
  IconButton,
  CardActions,
  CardActionArea,
  Drawer,
  Box,
  List,
  ListItem,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Divider,
  Container,
  SpeedDial,
  SpeedDialAction,
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import FormatListBulletedIcon from "@mui/icons-material/FormatListBulleted";
import SpeedDialIcon from "@mui/material/SpeedDialIcon";
import SettingsIcon from "@mui/icons-material/Settings";
import CloseIcon from "@mui/icons-material/Close";
import FlipCameraIosIcon from "@mui/icons-material/FlipCameraIos";
import {Link, useLocation, useNavigate, useNavigationType, useSearchParams} from "react-router";
import {useSnackbar} from "notistack";
import type {ReadResult} from "zxing-wasm/reader";
import ScannerBlock from "../../components/ScannerBlock/ScannerBlock.tsx";

export interface ScannerPageProps {}

function stripControlCharsKeepWhitespace(str: string) {
  // eslint-disable-next-line no-control-regex
  return str.replace(/[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]/g, "");
}

function ScannerPage({}: ScannerPageProps) {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const navType = useNavigationType();
  const location = useLocation();
  const {enqueueSnackbar, closeSnackbar} = useSnackbar();

  const [scanned, setScanned] = useState<string[]>([]);
  const [latestScanned, setLatestScanned] = useState<string | null>(null);

  const onScanned = useCallback(
    (barCodeTextData: string, barcodeRawData: DetectedBarcode | ReadResult) => {
      barCodeTextData = stripControlCharsKeepWhitespace(barCodeTextData);
      if (barCodeTextData != latestScanned) {
        console.log("barCodeTextData", barCodeTextData, barcodeRawData, latestScanned);
        enqueueSnackbar(`Scanned QR Data: ${barCodeTextData.slice(1, 10)}`);
        setLatestScanned(barCodeTextData);
        if (!scanned.includes(barCodeTextData)) {
          setScanned((s) => [...s, barCodeTextData]);
        }
      }
    },
    [latestScanned, enqueueSnackbar, scanned],
  );

  const scannedCodesDrawerOpen = searchParams.get("scannedCodesDrawerOpen") == "true";

  const setScannedCodesDrawerOpen = (open: boolean) => {
    console.log("setScannedCodesDrawerOpen", open);
    if (open) {
      navigate(`?scannedCodesDrawerOpen=true`);
    } else {
      navigate("", {replace: true});
    }
  };

  console.log(scannedCodesDrawerOpen, location.search, navType);

  const DrawerList = (
    <Box sx={{height: "85vh"}} role="presentation">
      <List>
        {scanned.map((text, index) => (
          <ListItem key={text} disablePadding>
            <ListItemButton>
              <ListItemIcon>
                <FormatListBulletedIcon />
              </ListItemIcon>
              <ListItemText sx={{wordBreak: "break-all"}} primary={text} />
            </ListItemButton>
          </ListItem>
        ))}
      </List>
    </Box>
  );

  return (
    <>
      <ScannerPageWrapper>
        <FullscreenWrapper>
          <ScannerBlock onScanned={onScanned} />
        </FullscreenWrapper>
        <FullscreenWrapper sx={{pointerEvents: "none"}}>
          <Container sx={{paddingTop: 2}}>
            <Stack direction={"row"} spacing={1}>
              <Fab
                sx={{pointerEvents: "auto"}}
                size="small"
                onClick={() => navigate("/", {replace: true})}
              >
                <ArrowBackIcon />
              </Fab>
              <Card
                sx={(theme) => ({
                  pointerEvents: "auto",
                  flex: 1,
                  backgroundColor: theme.palette.grey[300],
                  zIndex: 1,
                })}
              >
                <CardActionArea onClick={() => setScannedCodesDrawerOpen(true)}>
                  <CardContent
                    sx={{
                      paddingTop: 0.5,
                      paddingBottom: 0.5,
                      paddingLeft: 1,
                      paddingRight: 1,
                    }}
                  >
                    <Stack
                      direction={"row"}
                      sx={{
                        justifyContent: "space-between",
                        alignItems: "center",
                      }}
                    >
                      <Stack direction={"row"} spacing={0.5} sx={{alignItems: "center"}}>
                        <Typography variant={"body1"}>Отсканировано:</Typography>
                        <Typography variant={"h5"}>{scanned.length}</Typography>
                      </Stack>
                      <FormatListBulletedIcon />
                    </Stack>
                  </CardContent>
                  <div />
                </CardActionArea>
              </Card>
            </Stack>
          </Container>
        </FullscreenWrapper>
      </ScannerPageWrapper>
      <Drawer
        open={scannedCodesDrawerOpen}
        onClose={() => setScannedCodesDrawerOpen(false)}
        anchor={"bottom"}
      >
        {DrawerList}
      </Drawer>
    </>
  );
}

export default ScannerPage;

const ScannerPageWrapper = styled("div")(
  ({theme}) => css`
    width: 100%;
    height: 100%;
    position: relative;
    overflow: hidden;
    display: grid;
  `,
);

const FullscreenWrapper = styled("div")(
  ({theme}) => css`
    width: 100%;
    height: 100%;
    grid-column: 1;
    grid-row: 1;
    overflow: hidden;
  `,
);
