import {
  Chip,
  FormControlLabel,
  Stack,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TableSortLabel,
  Tooltip,
} from "@mui/material";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";
import {useSnackbar} from "notistack";
import {
  marketplacesGetAccountQueryKey,
  marketplacesGetWarehousesOptions,
  marketplacesSetWarehouseMappingMutation,
} from "@/api/@tanstack/react-query.gen";
import {extractErrorMessage} from "@/utils/errorUtils";
import {usePaginatedParams} from "@/hooks/usePaginatedParams";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {useTableSort} from "@/hooks/useTableSort";
import {useHasPermission} from "@/hooks/usePermission";
import FiltersBar from "@/components/FiltersBar";
import DataTableContainer from "@/components/DataTableContainer";
import TableRowLoader from "@/components/TableRowLoader";
import TableRowEmpty from "@/components/TableRowEmpty";
import WarehousesSelect from "@/components/WarehousesSelect";
import WarehouseStatusChip from "../../components/WarehouseStatusChip";
import {WAREHOUSE_KIND_LABELS} from "../../marketplaceUtils";
import type {MarketplaceWarehouseSortBy} from "@/api/types.gen";

const SORT_COLUMNS: {key: MarketplaceWarehouseSortBy; label: string}[] = [
  {key: "name", label: "Склад маркетплейса"},
  {key: "kind", label: "Тип"},
  {key: "syncedAt", label: "Обновлён"},
];

interface AccountWarehousesTabProps {
  accountId: string;
}

function AccountWarehousesTab({accountId}: AccountWarehousesTabProps) {
  const queryClient = useQueryClient();
  const {enqueueSnackbar} = useSnackbar();
  const canMap = useHasPermission("integrations.map");

  const [includeArchived, setIncludeArchived] = useSyncedWithQueryState(
    "archived",
    (q) => q === "true",
    (v) => (v ? "true" : null),
  );

  const {sortBy, sortOrder, handleSortClick} = useTableSort(SORT_COLUMNS, "name");

  const {fetchParams, page, setPage, pageSize, setPageSize} = usePaginatedParams(
    {},
    [],
    {includeArchived, sortBy, sortOrder},
    [includeArchived, sortBy, sortOrder],
    {defaultPageSize: 50},
  );

  const listQueryOptions = marketplacesGetWarehousesOptions({
    path: {id: accountId},
    query: fetchParams,
  });
  const {data, isLoading, isFetching} = useQuery(listQueryOptions);

  const mappingMutation = useMutation({
    ...marketplacesSetWarehouseMappingMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: async () => {
      await queryClient.invalidateQueries({queryKey: listQueryOptions.queryKey});
      await queryClient.invalidateQueries({
        queryKey: marketplacesGetAccountQueryKey({path: {id: accountId}}),
      });
    },
    onError: (err) =>
      enqueueSnackbar(extractErrorMessage(err) || "Не удалось сохранить привязку", {
        variant: "error",
      }),
  });

  return (
    <Stack spacing={2}>
      <FiltersBar>
        <FormControlLabel
          control={
            <Switch
              checked={includeArchived}
              onChange={(e) => setIncludeArchived(e.target.checked)}
            />
          }
          label="Показывать архивные"
        />
      </FiltersBar>
      <DataTableContainer
        isFetching={isFetching}
        count={data?.total ?? 0}
        page={page}
        onPageChange={setPage}
        rowsPerPage={pageSize}
        onRowsPerPageChange={setPageSize}
      >
        <Table size="small">
          <TableHead>
            <TableRow>
              {SORT_COLUMNS.map(({key, label}) => (
                <TableCell key={key} sortDirection={sortBy === key ? sortOrder : false}>
                  <TableSortLabel
                    active={sortBy === key}
                    direction={sortBy === key ? sortOrder : "asc"}
                    onClick={() => handleSortClick(key)}
                  >
                    {label}
                  </TableSortLabel>
                </TableCell>
              ))}
              <TableCell>Статус</TableCell>
              <TableCell>Адрес</TableCell>
              <TableCell sx={{minWidth: 280}}>Склад WMS</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {isLoading ? (
              <TableRowLoader colSpan={6} />
            ) : data?.items.length === 0 ? (
              <TableRowEmpty colSpan={6} message="Склады не синхронизированы" />
            ) : (
              data?.items.map((warehouse) => (
                <TableRow key={warehouse.id} hover>
                  <TableCell>
                    <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
                      {!warehouse.warehouseId && warehouse.status == "active" && (
                        <Tooltip title="Без привязки к складу WMS синхронизация заказов не определит место сборки">
                          <WarningAmberIcon color="warning" sx={{fontSize: 18}} />
                        </Tooltip>
                      )}
                      <span>{warehouse.name}</span>
                      {warehouse.isArchived && (
                        <Chip label="Архивный" size="small" variant="outlined" />
                      )}
                    </Stack>
                  </TableCell>
                  <TableCell>{WAREHOUSE_KIND_LABELS[warehouse.kind]}</TableCell>
                  <TableCell>{new Date(warehouse.syncedAt).toLocaleDateString("ru-RU")}</TableCell>
                  <TableCell>
                    <WarehouseStatusChip
                      status={warehouse.status}
                      externalStatus={warehouse.externalStatus}
                    />
                  </TableCell>
                  <TableCell>{warehouse.address ?? "—"}</TableCell>
                  <TableCell>
                    <WarehousesSelect
                      value={warehouse.warehouseId ?? null}
                      onChange={(warehouseId) =>
                        mappingMutation.mutate({
                          path: {id: warehouse.id},
                          body: {warehouseId},
                        })
                      }
                      disabled={!canMap || mappingMutation.isPending}
                      size="small"
                      textFieldProps={{label: "Склад WMS"}}
                      fullWidth
                    />
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </DataTableContainer>
    </Stack>
  );
}

export default AccountWarehousesTab;
