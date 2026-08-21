import {useCallback, useState} from "react";
import {Link, useParams} from "react-router";
import {
  Box,
  Button,
  Chip,
  CircularProgress,
  Divider,
  Paper,
  Skeleton,
  Stack,
  Typography,
} from "@mui/material";
import {useQuery, useQueryClient} from "@tanstack/react-query";
import {byOperation} from "@/utils/queryKeys";
import {useStaleData} from "@/hooks/useStaleData";
import {
  warehousesGetByIdForPrintOptions,
  warehousesGetByIdOptions,
} from "@/api/@tanstack/react-query.gen";
import EditIcon from "@mui/icons-material/Edit";
import Inventory2Icon from "@mui/icons-material/Inventory2";
import {isNotFoundError} from "@/utils/errorUtils";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import PageGenericHeader from "@/components/PageGenericHeader";
import NotFound from "@/components/NotFound";
import QueryError from "@/components/QueryError";
import {green} from "@mui/material/colors";
import StoragePlaceDrawer from "@/pages/WarehousesPage/pages/WarehouseViewPage/StoragePlaceDrawer.tsx";
import PrintIcon from "@mui/icons-material/Print";
import {openPrintPage} from "@/utils/printUtils.ts";
import {formatEntityBarcode} from "@/utils/barcodeUtils.ts";
import {useHasPermission} from "@/hooks/usePermission.ts";
import {useDrawerSearchParamsState} from "@/hooks/useDrawerSearchParamsState.ts";
import {WarehouseCanvas} from "@/features/warehouse";
import {formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";
import LoadingOverlay from "@/components/LoadingOverlay";
import {MARKETPLACE_TYPE_COLORS} from "@/pages/SettingsPage/pages/MarketplacesSettingsPage/marketplaceUtils.ts";

function WarehouseViewPage() {
  const [isPrinting, setIsPrinting] = useState(false);

  const userCanEdit = useHasPermission(["warehouses.edit", "warehouses.edit_assigned"]);

  const [selectedStoragePlace, openStoragePlaceDialog, closeStoragePlaceDialog] =
    useDrawerSearchParamsState("storagePlace");

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
          value: formatEntityBarcode("storagePlaceNode", node.id),
          label: formatStoragePlaceNodeName(node.name),
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
    isFetching,
    isError,
    isRefetchError,
    error,
    dataUpdatedAt,
  } = useQuery({
    ...warehousesGetByIdOptions({path: {id: id!}}),
    meta: {suppressGlobalError: true, suppressGlobalNotFound: true},
  });

  // Read-only, so there is nothing to lose: the warning never becomes a banner, the data just reloads.
  const refreshWarehouse = useCallback(() => {
    void queryClient.invalidateQueries({
      queryKey: byOperation("warehousesGetById", {path: {id: id!}}),
    });
  }, [queryClient, id]);

  const {showLoadingOverlay} = useStaleData("warehouse", id, {
    dataUpdatedAt,
    isFetching,
    isLoading,
    onRefresh: refreshWarehouse,
  });

  const defaultNodeId = warehouse?.defaultStoragePlaceNodeId;

  const printQuery = useQuery({
    ...warehousesGetByIdForPrintOptions({path: {id: id!}}),
    enabled: !!defaultNodeId,
    meta: {suppressGlobalError: true},
  });

  const defaultNodePath = (() => {
    if (!defaultNodeId || !printQuery.data) return null;
    const node = printQuery.data.find((n) => n.id === defaultNodeId);
    return node ? formatStoragePlaceNodeName(node.name) : null;
  })();

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
    <Box sx={{position: "relative"}}>
      <LoadingOverlay open={showLoadingOverlay} />
      <Stack spacing={2}>
        <AppBreadcrumbs
          path={[
            {name: "Склады", link: "/storage/warehouses"},
            {name: warehouse.name},
            {name: "Просмотр"},
          ]}
          viewersOf={{entityType: "warehouse", entityId: id}}
        />
        <PageGenericHeader
          title={warehouse.name}
          right={
            <>
              <Button
                variant="outlined"
                startIcon={<Inventory2Icon />}
                component={Link}
                to={`/storage/warehouses/${id}/inventory`}
              >
                Остатки
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
                  to={`/storage/warehouses/${id}/edit`}
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
              {label: "Длина", value: `${warehouse.height} м`},
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
            {defaultNodeId && (
              <Stack spacing={0.25}>
                <Typography variant="caption" color="text.secondary">
                  Ячейка по умолчанию
                </Typography>
                {printQuery.isLoading ? (
                  <Skeleton width={160} />
                ) : (
                  <Typography variant="body1" sx={{fontWeight: 500}}>
                    {defaultNodePath ?? "—"}
                  </Typography>
                )}
              </Stack>
            )}
            {warehouse.marketplaceAccounts.length > 0 && (
              <Stack spacing={0.25}>
                <Typography variant="caption" color="text.secondary">
                  Привязано к складам маркетплейсов
                </Typography>
                <Stack spacing={1} direction="row">
                  {warehouse.marketplaceAccounts.map((account) => (
                    <Chip
                      component={Link}
                      to={`/settings/integrations/${account.id}?tab=warehouses`}
                      color={MARKETPLACE_TYPE_COLORS[account.type]}
                      onClick={() => {}}
                      label={account.name}
                      size="small"
                    />
                  ))}
                </Stack>
              </Stack>
            )}
          </Stack>
        </Paper>

        <Paper sx={{width: "100%", height: "calc(100vh - 300px)", position: "relative"}}>
          <WarehouseCanvas
            width={warehouse.width}
            height={warehouse.height}
            layoutObjects={warehouse.layoutObjects}
            storagePlaces={warehouse.storagePlaces.map((p) => ({
              ...p,
              fill: green[300],
              label: p.totalItemsCount > 0 ? `${p.name}\n${p.totalItemsCount} тов.` : p.name,
            }))}
            onStoragePlaceClick={openStoragePlaceDialog}
          />
        </Paper>

        <StoragePlaceDrawer
          open={!!selectedStoragePlace}
          onClose={() => closeStoragePlaceDialog()}
          storagePlace={warehouse?.storagePlaces.find((x) => x.id === selectedStoragePlace)}
          warehouseId={id!}
        />
      </Stack>
    </Box>
  );
}

export default WarehouseViewPage;
