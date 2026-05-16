import React, {useEffect, useRef, useState} from "react";
import {Link, useParams} from "react-router";
import {
  Box,
  Button,
  CircularProgress,
  Divider,
  IconButton,
  Paper,
  Stack,
  Tooltip,
  Typography,
} from "@mui/material";
import {useQuery, useQueryClient} from "@tanstack/react-query";
import {
  warehousesGetByIdForPrintOptions,
  warehousesGetByIdOptions,
} from "@/api/@tanstack/react-query.gen";
import ListAltIcon from "@mui/icons-material/ListAlt";
import EditIcon from "@mui/icons-material/Edit";
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
import {blue, green, grey, orange} from "@mui/material/colors";
import {type WarehouseLayoutObjectType} from "@/api/types.gen.ts";
import StoragePlaceDialog from "@/pages/WarehousesPage/pages/WarehouseViewPage/StoragePlaceDialog.tsx";
import PrintIcon from "@mui/icons-material/Print";
import {openPrintPage} from "@/utils/printUtils.ts";
import {useHasPermission} from "@/hooks/usePermission.ts";

const layoutObjectStyle: Record<WarehouseLayoutObjectType, {fill: string; stroke: string}> = {
  wall: {fill: grey[700], stroke: grey[800]},
  passage: {fill: orange[100], stroke: orange[300]},
};

function WarehouseViewPage() {
  const containerRef = useRef<HTMLDivElement>(null);
  const stageRef = useRef<StageWithPanAndZoomHandle>(null);

  const [stageScale, setStageScale] = useState({x: 1, y: 1});

  const [selectedStoragePlace, setSelectedStoragePlace] = useState<string | null>(null);
  const [storagePlaceDialogOpen, setStoragePlaceDialogOpen] = useState(false);
  const [isPrinting, setIsPrinting] = useState(false);

  const userCanEdit = useHasPermission("warehouses.edit");

  const openStoragePlaceDialog = (storagePlace: string) => {
    setStoragePlaceDialogOpen(true);
    setSelectedStoragePlace(storagePlace);
  };

  const queryClient = useQueryClient();

  const printLabels = async () => {
    setIsPrinting(true);
    try {
      const data = await queryClient.fetchQuery(
        warehousesGetByIdForPrintOptions({path: {id: id!}}),
      );
      openPrintPage(
        data.map((node) => ({
          type: "DataMatrix" as const,
          value: node.id,
          label: node.name.join(" / "),
        })),
      );
    } catch {
      // handled by QueryErrorHandler
    } finally {
      setIsPrinting(false);
    }
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

    requestAnimationFrame(() => stageRef.current?.fit());
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
              component={Link}
              to={`/warehouses/${id}/items`}
              startIcon={<ListAltIcon />}
              variant="outlined"
            >
              Список товаров
            </Button>
            <Button
              startIcon={isPrinting ? <CircularProgress size={14} /> : <PrintIcon />}
              disabled={isPrinting}
              onClick={printLabels}
              variant="outlined"
            >
              Этикетки
            </Button>
            {userCanEdit && (
              <Button
                variant="outlined"
                startIcon={<EditIcon />}
                component={Link}
                to={`/warehouses/${id}/edit`}
              >
                Редактировать
              </Button>
            )}
          </>
        }
      />

      <Paper sx={{px: 3, py: 2}}>
        <Stack
          direction="row"
          spacing={3}
          useFlexGap
          sx={{flexWrap: "wrap"}}
          divider={<Divider orientation="vertical" flexItem />}
        >
          {[
            {label: "Ширина", value: `${warehouse.width} м`},
            {label: "Высота", value: `${warehouse.height} м`},
            {label: "Мест хранения", value: String(warehouse.storagePlaces.length)},
            {label: "Товаров", value: String(warehouse.totalItemsCount)},
          ].map(({label, value}) => (
            <Stack key={label} spacing={0.25}>
              <Typography variant="caption" color="text.secondary">
                {label}
              </Typography>
              <Typography variant="body1" sx={{fontWeight: 500}}>
                {value}
              </Typography>
            </Stack>
          ))}
        </Stack>
      </Paper>

      <Paper
        ref={containerRef}
        sx={{width: "100%", height: "calc(100vh - 300px)", position: "relative"}}
      >
        <StageWithPanAndZoom
          containerRef={containerRef}
          ref={stageRef}
          setStageScale={setStageScale}
        >
          <Rect
            x={0}
            y={0}
            width={warehouse.width * 100}
            height={warehouse.height * 100}
            stroke={blue[300]}
            dash={[10 / stageScale.x]}
          />
          {warehouse.layoutObjects.map((lo, i) => (
            <Rect
              key={i}
              x={lo.x * 100 + (lo.width * 100) / 2}
              y={lo.y * 100 + (lo.height * 100) / 2}
              offsetX={(lo.width * 100) / 2}
              offsetY={(lo.height * 100) / 2}
              width={lo.width * 100}
              height={lo.height * 100}
              rotation={lo.rotation}
              {...layoutObjectStyle[lo.type]}
            />
          ))}
          {warehouse.storagePlaces.map((p) => (
            <React.Fragment key={p.id}>
              <Rect
                x={p.x * 100 + (p.width * 100) / 2}
                y={p.y * 100 + (p.height * 100) / 2}
                width={p.width * 100}
                height={p.height * 100}
                offsetX={(p.width * 100) / 2}
                offsetY={(p.height * 100) / 2}
                fill={green[300]}
                onClick={() => openStoragePlaceDialog(p.id)}
                onTap={() => openStoragePlaceDialog(p.id)}
                rotation={p.rotation}
              />
              <Text
                x={p.x * 100 + (p.width * 100) / 2}
                y={p.y * 100 + (p.height * 100) / 2}
                width={p.width * 100}
                height={p.height * 100}
                offsetX={(p.width * 100) / 2}
                offsetY={(p.height * 100) / 2}
                align="center"
                verticalAlign="middle"
                text={p.totalItemsCount > 0 ? `${p.name}\n${p.totalItemsCount} тов.` : p.name}
                onClick={() => openStoragePlaceDialog(p.id)}
                onTap={() => openStoragePlaceDialog(p.id)}
                rotation={p.rotation}
              />
            </React.Fragment>
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
        warehouseId={id!}
      />
    </Stack>
  );
}

export default WarehouseViewPage;
