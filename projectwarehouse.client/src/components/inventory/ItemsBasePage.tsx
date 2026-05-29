import {
  FormControl,
  IconButton,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TableSortLabel,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from "@mui/material";
import ArchiveIcon from "@mui/icons-material/Archive";
import StarIcon from "@mui/icons-material/Star";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import RefreshIcon from "@mui/icons-material/Refresh";
import PageGenericHeader from "@/components/PageGenericHeader";
import {useQuery} from "@tanstack/react-query";
import {
  inventoryItemsGetAllOptions,
  warehousesGetAllOptions,
} from "@/api/@tanstack/react-query.gen";
import {
  type CatalogItemType,
  type InventoryItemSortBy,
  type InventoryItemSummaryDto,
} from "@/api/types.gen";
import {CATALOG_ITEM_TYPE_CONFIG, CATALOG_ITEM_TYPES} from "@/features/catalog";
import {useDebouncedSyncedWithQueryState} from "@/hooks/useDebouncedSyncedWithQueryState";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {usePaginatedParams} from "@/hooks/usePaginatedParams";
import {useDrawerSearchParamsState} from "@/hooks/useDrawerSearchParamsState";
import {useTableSort} from "@/hooks/useTableSort";
import DataTableContainer from "@/components/DataTableContainer";
import SearchInput from "@/components/SearchInput";
import FiltersBar from "@/components/FiltersBar";
import TableRowLoader from "@/components/TableRowLoader";
import TableRowEmpty from "@/components/TableRowEmpty";
import CatalogItemTypeChip from "@/components/catalog/CatalogItemTypeChip";
import {CatalogItemDrawer} from "@/components/catalog/CatalogItemDrawer";
import {UnitItemsDrawer} from "@/components/inventory/UnitItemsDrawer";
import {AssembledBundleItemsDrawer} from "@/components/inventory/AssembledBundleItemsDrawer";

const SORTABLE_COLUMNS: {key: InventoryItemSortBy; label: string}[] = [
  {key: "type", label: "Тип"},
  {key: "fullName", label: "Название"},
  {key: "article", label: "Артикул"},
  {key: "count", label: "Количество"},
];

interface ItemsBasePageProps {
  title: string;
  warehouseId?: string;
  storagePlaceId?: string;
  nodeId?: string;
}

function ItemsBasePage({title, warehouseId, storagePlaceId, nodeId}: ItemsBasePageProps) {
  const showWarehouseFilter = !warehouseId;

  const [inputValue, setInputValue, searchString] = useDebouncedSyncedWithQueryState(
    "search",
    (q) => (typeof q === "string" ? q : ""),
    (v) => v || null,
  );

  const {sortBy, sortOrder, handleSortClick} = useTableSort(SORTABLE_COLUMNS, "name");

  const [itemType, setItemType] = useSyncedWithQueryState<CatalogItemType | null>(
    "type",
    (q) => (CATALOG_ITEM_TYPES.includes(q as CatalogItemType) ? (q as CatalogItemType) : null),
    (v) => v ?? null,
  );

  const [isArchived, setIsArchived] = useSyncedWithQueryState<boolean | null>(
    "archived",
    (q) => (q === "true" ? true : q === "false" ? false : null),
    (v) => (v === null ? null : String(v)),
  );

  const [filterWarehouseId, setFilterWarehouseId] = useSyncedWithQueryState<string | null>(
    "warehouse",
    (q) => (typeof q === "string" ? q : null),
    (v) => v,
  );

  const effectiveWarehouseId = warehouseId ?? filterWarehouseId ?? undefined;

  const {fetchParams, page, setPage, pageSize, setPageSize} = usePaginatedParams(
    {},
    [],
    {
      searchString,
      warehouseId: effectiveWarehouseId,
      storagePlaceId,
      nodeId,
      catalogItemType: itemType ?? undefined,
      isArchived: isArchived ?? undefined,
      sortBy,
      sortOrder,
    },
    [
      searchString,
      effectiveWarehouseId,
      storagePlaceId,
      nodeId,
      itemType,
      isArchived,
      sortBy,
      sortOrder,
    ],
  );

  const {data, isLoading, isFetching, refetch} = useQuery(
    inventoryItemsGetAllOptions({query: fetchParams}),
  );

  const {data: warehousesData} = useQuery({
    ...warehousesGetAllOptions({query: {pageSize: 200}}),
    enabled: showWarehouseFilter,
  });

  const [catalogItemId, openCatalogDrawer, closeCatalogDrawer] =
    useDrawerSearchParamsState("catalogItem");
  const [unitCatalogItemId, openUnitDrawer, closeUnitDrawer] =
    useDrawerSearchParamsState("unitCatalogItem");
  const [bundleCatalogItemId, openBundleDrawer, closeBundleDrawer] =
    useDrawerSearchParamsState("bundleCatalogItem");

  const getRowClickHandler = (row: InventoryItemSummaryDto) => {
    if (row.catalogItem.type === "unit") return () => openUnitDrawer(row.catalogItemId);
    if (row.catalogItem.type === "assembledBundle")
      return () => openBundleDrawer(row.catalogItemId);
    return undefined;
  };

  const unitItem = data?.items.find((i) => i.catalogItemId === unitCatalogItemId);
  const bundleItem = data?.items.find((i) => i.catalogItemId === bundleCatalogItemId);

  return (
    <>
      <Stack spacing={2}>
        <PageGenericHeader
          title={title}
          right={
            <IconButton color="inherit" onClick={() => refetch()}>
              <RefreshIcon />
            </IconButton>
          }
        >
          <SearchInput value={inputValue} onChange={setInputValue} />
        </PageGenericHeader>

        <FiltersBar>
          {showWarehouseFilter && (
            <FormControl size="small" sx={{minWidth: 200}}>
              <InputLabel>Склад</InputLabel>
              <Select
                label="Склад"
                value={filterWarehouseId ?? ""}
                onChange={(e) => setFilterWarehouseId(e.target.value || null)}
              >
                <MenuItem value="">Все склады</MenuItem>
                {warehousesData?.items.map((w) => (
                  <MenuItem key={w.id} value={w.id}>
                    {w.name}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          )}

          <FormControl size="small" sx={{minWidth: 150}}>
            <InputLabel>Тип</InputLabel>
            <Select
              label="Тип"
              value={itemType ?? ""}
              onChange={(e) => setItemType((e.target.value as CatalogItemType) || null)}
            >
              <MenuItem value="">Все типы</MenuItem>
              {CATALOG_ITEM_TYPES.map((type) => (
                <MenuItem key={type} value={type}>
                  {CATALOG_ITEM_TYPE_CONFIG[type].label}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          <ToggleButtonGroup
            exclusive
            size="small"
            value={isArchived ?? null}
            onChange={(_, v: boolean | null) => setIsArchived(v)}
          >
            <ToggleButton value={false} sx={{gap: 0.5}}>
              <StarIcon fontSize="small" />
              Активные
            </ToggleButton>
            <ToggleButton value={true} sx={{gap: 0.5}}>
              <ArchiveIcon fontSize="small" />
              Архивные
            </ToggleButton>
          </ToggleButtonGroup>
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
                {SORTABLE_COLUMNS.map(({key, label}) => (
                  <TableCell key={key} align={key === "count" ? "right" : "left"}>
                    <TableSortLabel
                      active={sortBy === key}
                      direction={sortBy === key ? sortOrder : "asc"}
                      onClick={() => handleSortClick(key)}
                    >
                      {label}
                    </TableSortLabel>
                  </TableCell>
                ))}
              </TableRow>
            </TableHead>
            <TableBody>
              {isLoading ? (
                <TableRowLoader colSpan={4} />
              ) : data?.items.length === 0 ? (
                <TableRowEmpty colSpan={4} message="Позиции не найдены" />
              ) : (
                data?.items.map((row) => {
                  const handleRowClick = getRowClickHandler(row);
                  const isClickable = !!handleRowClick;
                  return (
                    <TableRow
                      key={row.catalogItemId}
                      hover={isClickable}
                      selected={
                        row.catalogItemId === unitCatalogItemId ||
                        row.catalogItemId === bundleCatalogItemId
                      }
                      sx={{
                        cursor: isClickable ? "pointer" : "default",
                        opacity: isFetching && !isLoading ? 0.5 : 1,
                        transition: "opacity 0.2s",
                      }}
                      onClick={handleRowClick}
                    >
                      <TableCell sx={{width: 110}}>
                        <CatalogItemTypeChip type={row.catalogItem.type} />
                      </TableCell>
                      <TableCell>
                        <Stack
                          direction="row"
                          spacing={1}
                          sx={{
                            alignItems: "center",
                            cursor: "pointer",
                            width: "fit-content",
                            "& .open-icon": {visibility: "hidden"},
                            "&:hover .open-icon": {visibility: "visible"},
                          }}
                          onClick={(e) => {
                            e.stopPropagation();
                            openCatalogDrawer(row.catalogItemId);
                          }}
                        >
                          <Typography variant="body2">{row.catalogItem.fullName}</Typography>
                          {row.catalogItem.isArchived && (
                            <ArchiveIcon
                              sx={{fontSize: 14, color: "warning.main", flexShrink: 0}}
                            />
                          )}
                          <OpenInNewIcon
                            className="open-icon"
                            sx={{fontSize: 14, color: "text.secondary"}}
                          />
                        </Stack>
                      </TableCell>
                      <TableCell>{row.catalogItem.article}</TableCell>
                      <TableCell align="right">
                        <Typography variant="body2" sx={{fontWeight: 500}}>
                          {row.count}
                        </Typography>
                      </TableCell>
                    </TableRow>
                  );
                })
              )}
            </TableBody>
          </Table>
        </DataTableContainer>
      </Stack>

      <CatalogItemDrawer
        itemId={catalogItemId}
        onClose={closeCatalogDrawer}
        onOpenItem={openCatalogDrawer}
      />

      <UnitItemsDrawer
        catalogItemId={unitCatalogItemId}
        catalogItemName={unitItem?.catalogItem.fullName}
        warehouseId={effectiveWarehouseId}
        storagePlaceId={storagePlaceId}
        nodeId={nodeId}
        onClose={closeUnitDrawer}
      />

      <AssembledBundleItemsDrawer
        catalogItemId={bundleCatalogItemId}
        catalogItemName={bundleItem?.catalogItem.fullName}
        warehouseId={effectiveWarehouseId}
        storagePlaceId={storagePlaceId}
        nodeId={nodeId}
        onClose={closeBundleDrawer}
      />
    </>
  );
}

export default ItemsBasePage;
