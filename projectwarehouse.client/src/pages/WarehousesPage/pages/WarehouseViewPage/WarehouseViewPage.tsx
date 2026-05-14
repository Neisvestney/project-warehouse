import React, {useEffect, useRef, useState} from "react";
import {Link as RouterLink, useParams} from "react-router";
import {Box, Button, CircularProgress, IconButton, Paper, Stack, Tooltip} from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import {useQuery} from "@tanstack/react-query";
import {warehousesGetByIdOptions} from "@/api/@tanstack/react-query.gen";
import {isNotFoundError} from "@/utils/errorUtils";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import PageGenericHeader from "@/components/PageGenericHeader";
import NotFound from "@/components/NotFound";
import QueryError from "@/components/QueryError";
import MyLocationIcon from "@mui/icons-material/MyLocation";
import StageWithPanAndZoom, {
  type StageWithPanAndZoomHandle,
} from "@/components/StageWithPanAndZoom.tsx";
import {Rect, Text} from "react-konva";
import {blue, green} from "@mui/material/colors";
import StoragePlaceDialog from "@/pages/WarehousesPage/pages/WarehouseViewPage/StoragePlaceDialog.tsx";

function WarehouseViewPage() {
  const containerRef = useRef<HTMLDivElement>(null);
  const stageRef = useRef<StageWithPanAndZoomHandle>(null);

  const [stageScale, setStageScale] = useState({x: 1, y: 1});

  const [selectedStoragePlace, setSelectedStoragePlace] = useState<string | null>(null);
  const [storagePlaceDialogOpen, setStoragePlaceDialogOpen] = useState(false);

  const openStoragePlaceDialog = (storagePlace: string) => {
    setStoragePlaceDialogOpen(true);
    setSelectedStoragePlace(storagePlace);
  };

  const {id} = useParams<{id: string}>();

  const {
    data: warehouse,
    isLoading,
    isError,
    isRefetchError,
    error,
  } = useQuery({
    ...warehousesGetByIdOptions({path: {id: id!}}),
    meta: {suppressGlobalError: true, suppressGlobalNotFound: true},
  });

  const fitted = useRef(false);
  useEffect(() => {
    if (!warehouse || isLoading) return;
    if (!stageRef.current) return;
    if (fitted.current) return;
    fitted.current = true;

    const timeout = setTimeout(() => {
      stageRef.current?.fit();
    });

    return () => clearTimeout(timeout);
  }, [warehouse, isLoading]);

  if (isLoading) {
    return (
      <Box sx={{display: "flex", justifyContent: "center", pt: 8}}>
        <CircularProgress />
      </Box>
    );
  }

  if (isError && !isRefetchError)
    return isNotFoundError(error) ? <NotFound /> : <QueryError error={error} />;
  if (!warehouse) return <NotFound />;

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs
        path={[{name: "Склады", link: "/warehouses"}, {name: warehouse.name}, {name: "Просмотр"}]}
      />
      <PageGenericHeader
        title={warehouse.name}
        right={
          <>
            <Button
              variant="outlined"
              startIcon={<EditIcon />}
              component={RouterLink}
              to={`/warehouses/${id}/edit`}
              disabled
            >
              Редактировать
            </Button>
          </>
        }
      />

      <Paper
        ref={containerRef}
        sx={{width: "100%", height: "calc(100vh - 250px)", position: "relative"}}
      >
        <StageWithPanAndZoom
          containerRef={containerRef}
          ref={stageRef}
          setStageScale={setStageScale}
        >
          <Rect
            x={0}
            y={0}
            width={warehouse.width}
            height={warehouse.height}
            stroke={blue[300]}
            dash={[10 / stageScale.x]}
          />
          {warehouse.storagePlaces.map((p) => (
            <>
              <Rect
                x={p.x}
                y={p.y}
                width={p.width}
                height={p.height}
                fill={green[300]}
                onClick={() => openStoragePlaceDialog(p.id)}
                onTap={() => openStoragePlaceDialog(p.id)}
              />
              <Text
                x={p.x}
                y={p.y}
                width={p.width}
                height={p.height}
                align="center"
                verticalAlign="middle"
                text={p.name}
                onClick={() => openStoragePlaceDialog(p.id)}
                onTap={() => openStoragePlaceDialog(p.id)}
              ></Text>
            </>
          ))}
        </StageWithPanAndZoom>
        <Stack sx={{position: "absolute", top: 10, right: 10}}>
          <Tooltip title={"Отцентровать"}>
            <IconButton onClick={() => stageRef.current?.fit()}>
              <MyLocationIcon />
            </IconButton>
          </Tooltip>
        </Stack>
      </Paper>

      <StoragePlaceDialog
        open={!!storagePlaceDialogOpen}
        onClose={() => setStoragePlaceDialogOpen(false)}
        storagePlace={warehouse?.storagePlaces.find((x) => x.id === selectedStoragePlace)}
      />
    </Stack>
  );
}

export default WarehouseViewPage;
