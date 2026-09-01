import {useEffect, useState} from "react";
import {
  Box,
  Button,
  IconButton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TableSortLabel,
  ToggleButton,
  ToggleButtonGroup,
  Tooltip,
  Typography,
  Checkbox,
  FormControlLabel,
} from "@mui/material";
import ArchiveIcon from "@mui/icons-material/Archive";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import StarIcon from "@mui/icons-material/Star";
import RefreshIcon from "@mui/icons-material/Refresh";
import SettingsIcon from "@mui/icons-material/Settings";
import EditIcon from "@mui/icons-material/Edit";
import PushPinIcon from "@mui/icons-material/PushPin";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import {useQuery} from "@tanstack/react-query";
import {
  stockForecastGetListOptions,
  warehousesGetAllOptions,
} from "@/api/@tanstack/react-query.gen";
import type {StockForecastSortBy} from "@/api/types.gen";
import PageGenericHeader from "@/components/PageGenericHeader";
import DataTableContainer from "@/components/DataTableContainer";
import FiltersBar from "@/components/FiltersBar";
import SearchInput from "@/components/SearchInput";
import TableRowEmpty from "@/components/TableRowEmpty";
import TableRowLoader from "@/components/TableRowLoader";
import WarehousesSelect from "@/components/WarehousesSelect";
import CatalogItemTypeChip from "@/components/catalog/CatalogItemTypeChip";
import CatalogTagsFilter from "@/components/catalog/CatalogTagsFilter";
import CatalogTypesFilter from "@/components/catalog/CatalogTypesFilter";
import {CatalogItemDrawer} from "@/components/catalog/CatalogItemDrawer";
import {CatalogItemLink} from "@/components/catalog/CatalogItemLink";
import StockForecastChip from "@/components/forecast/StockForecastChip";
import {PHYSICAL_CATALOG_ITEMS, useCatalogTypesFilter} from "@/features/catalog";
import {useDebouncedSyncedWithQueryState} from "@/hooks/useDebouncedSyncedWithQueryState";
import {useDrawerSearchParamsState} from "@/hooks/useDrawerSearchParamsState";
import {usePaginatedParams} from "@/hooks/usePaginatedParams";
import {useHasPermission} from "@/hooks/usePermission";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {useTableSort} from "@/hooks/useTableSort";
import StockForecastSettingsDialog from "./StockForecastSettingsDialog";
import StockWarningOverrideDialog, {
  type StockWarningOverrideTarget,
} from "./StockWarningOverrideDialog";

const COLUMN_COUNT = 7;

const SORTABLE_COLUMNS: {key: StockForecastSortBy; label: string; align?: "right"}[] = [
  {key: "type", label: "Тип"},
  {key: "name", label: "Название"},
  {key: "article", label: "Артикул"},
  {key: "stock", label: "Остаток", align: "right"},
  {key: "dailyConsumption", label: "Расход/день", align: "right"},
  {key: "daysLeft", label: "Осталось дней", align: "right"},
];

interface ForecastBasePageProps {
  title: string;
  warehouseId?: string;
}

function ForecastBasePage({title, warehouseId}: ForecastBasePageProps) {
  const showWarehouseFilter = !warehouseId;
  const canEditWarehouse = useHasPermission(["warehouses.edit", "warehouses.edit_assigned"]);

  const [inputValue, setInputValue, searchString] = useDebouncedSyncedWithQueryState(
    "search",
    (q) => (typeof q === "string" ? q : ""),
    (v) => v || null,
  );

  // The composite `default` rule is the table's starting state, so it is not one of the clickable
  // columns; `clearable` gives it back on a third click, since nothing else can express it.
  const {sortBy, sortOrder, handleSortClick} = useTableSort<StockForecastSortBy>(
    SORTABLE_COLUMNS,
    "default",
    {clearable: true},
  );

  const [itemTypes, setItemTypes] = useCatalogTypesFilter("types", PHYSICAL_CATALOG_ITEMS);

  const [tagIds, setTagIds] = useSyncedWithQueryState<string[]>(
    "tags",
    (q) => (typeof q === "string" && q ? q.split(",").filter(Boolean) : []),
    (v) => v.join(",") || null,
  );

  const [isArchived, setIsArchived] = useSyncedWithQueryState<boolean | null>(
    "archived",
    (q) => (q === "true" ? true : q === "false" ? false : null),
    (v) => (v === null ? null : String(v)),
  );

  const [onlyWarnings, setOnlyWarnings] = useSyncedWithQueryState<boolean>(
    "warnings",
    (q) => q === "true",
    (v) => (v ? "true" : null),
  );

  const [filterWarehouseId, setFilterWarehouseId] = useSyncedWithQueryState<string | null>(
    "warehouse",
    (q) => (typeof q === "string" ? q : null),
    (v) => v,
  );

  const [accountForAssembly, setAccountForAssembly] = useSyncedWithQueryState<boolean>(
    "accountForAssembly",
    (q) => (typeof q === "string" ? q == "true" : false),
    (v) => (v ? "true" : null),
  );

  // One available warehouse means there is nothing to choose — the page would otherwise open empty.
  // Asked for whenever the select is shown: it must know whether clearing is meaningful even when
  // the warehouse arrived from the URL.
  const {data: warehousesData} = useQuery({
    ...warehousesGetAllOptions({query: {pageSize: 2}}),
    enabled: showWarehouseFilter,
  });
  const onlyWarehouseId =
    warehousesData?.total === 1 ? (warehousesData.items[0]?.id ?? null) : null;

  useEffect(() => {
    if (showWarehouseFilter && filterWarehouseId === null && onlyWarehouseId !== null)
      setFilterWarehouseId(onlyWarehouseId);
  }, [showWarehouseFilter, filterWarehouseId, onlyWarehouseId, setFilterWarehouseId]);

  const effectiveWarehouseId = warehouseId ?? filterWarehouseId;

  const {fetchParams, page, setPage, pageSize, setPageSize} = usePaginatedParams(
    {},
    [],
    {
      WarehouseId: effectiveWarehouseId ?? undefined,
      SearchString: searchString || undefined,
      CatalogItemTypes: itemTypes.length < PHYSICAL_CATALOG_ITEMS.length ? itemTypes : undefined,
      TagIds: tagIds.length > 0 ? tagIds : undefined,
      IsArchived: isArchived ?? undefined,
      OnlyWarnings: onlyWarnings || undefined,
      AccountForAssembly: accountForAssembly,
      SortBy: sortBy,
      SortOrder: sortOrder,
    },
    [
      effectiveWarehouseId,
      searchString,
      itemTypes,
      tagIds,
      isArchived,
      onlyWarnings,
      accountForAssembly,
      sortBy,
      sortOrder,
    ],
  );

  // Empty type selection can't be expressed server-side (no types == no filter), so match nothing here
  const noItemTypes = itemTypes.length === 0;

  const {
    data: queryData,
    isLoading,
    isFetching,
    refetch,
  } = useQuery({
    ...stockForecastGetListOptions({query: fetchParams}),
    enabled: effectiveWarehouseId !== null && !noItemTypes,
  });

  const data = noItemTypes ? undefined : queryData;

  const [catalogItemId, openCatalogDrawer, closeCatalogDrawer] =
    useDrawerSearchParamsState("catalogItem");

  const [settingsOpen, setSettingsOpen] = useState(false);
  const [overrideTarget, setOverrideTarget] = useState<StockWarningOverrideTarget | null>(null);

  // Settings applied to the numbers. They live in a tooltip on a permanently rendered icon: as a line
  // of their own they came and went with every query key and made the whole page jump.
  const appliedSettings = data && [
    `Расход за ${data.windowDays} дн.${data.useWeightedConsumption ? ", взвешенный" : ""}`,
    `Сутки по ${data.timeZoneId}`,
    `Порог склада ${data.warehouseWarningDays} дн.`,
  ];

  const emptyMessage =
    effectiveWarehouseId === null
      ? "Выберите склад"
      : noItemTypes
        ? "Типы не выбраны"
        : "Позиции не найдены";

  return (
    <>
      <Stack spacing={2}>
        <PageGenericHeader
          title={
            <>
              {title}
              <Tooltip
                title={
                  appliedSettings && appliedSettings.map((line) => <Box key={line}>{line}</Box>)
                }
              >
                <InfoOutlinedIcon
                  sx={{
                    // em keeps the icon scaled to whatever the header's own font size is.
                    fontSize: "0.7em",
                    verticalAlign: "middle",
                    ml: 0.5,
                    color: "primary.main",
                    opacity: appliedSettings ? 1 : 0,
                    pointerEvents: appliedSettings ? "auto" : "none",
                  }}
                />
              </Tooltip>
            </>
          }
          refresh={
            <Tooltip title="Обновить">
              <IconButton color="inherit" onClick={() => refetch()}>
                <RefreshIcon />
              </IconButton>
            </Tooltip>
          }
          actions={
            canEditWarehouse && (
              // A disabled button swallows its own events, so the tooltip needs a live wrapper.
              <Tooltip title={effectiveWarehouseId === null ? "Сначала выберите склад" : ""}>
                <span>
                  <Button
                    variant="outlined"
                    startIcon={<SettingsIcon />}
                    disabled={effectiveWarehouseId === null}
                    onClick={() => setSettingsOpen(true)}
                  >
                    Настройки склада
                  </Button>
                </span>
              </Tooltip>
            )
          }
        >
          <SearchInput value={inputValue} onChange={setInputValue} />
        </PageGenericHeader>

        <FiltersBar>
          {showWarehouseFilter && (
            <WarehousesSelect
              value={filterWarehouseId}
              onChange={setFilterWarehouseId}
              disableClearable={onlyWarehouseId !== null}
              size="small"
              sx={{minWidth: 220}}
            />
          )}

          <CatalogTypesFilter
            value={itemTypes}
            onChange={setItemTypes}
            options={PHYSICAL_CATALOG_ITEMS}
          />

          <CatalogTagsFilter
            value={tagIds}
            onChange={setTagIds}
            sx={{minWidth: 220, maxWidth: 420, flexGrow: 1}}
          />

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

          <ToggleButton
            size="small"
            value="onlyWarnings"
            selected={onlyWarnings}
            onChange={() => setOnlyWarnings(!onlyWarnings)}
            sx={{gap: 0.5}}
          >
            <WarningAmberIcon fontSize="small" />
            Только предупреждения
          </ToggleButton>
        </FiltersBar>

        <Stack direction="row" sx={{alignItems: "center"}} spacing={1}>
          <Tooltip title={"Вариативные товары не учитываются в любом случае"}>
            <span>
              <FormControlLabel
                control={
                  <Checkbox
                    checked={accountForAssembly}
                    onChange={(e) => setAccountForAssembly(e.target.checked)}
                  />
                }
                label="Учитывать заказы на сборке"
              />
            </span>
          </Tooltip>
        </Stack>

        <DataTableContainer
          isFetching={isFetching}
          count={data?.items.total ?? 0}
          page={page}
          onPageChange={setPage}
          rowsPerPage={pageSize}
          onRowsPerPageChange={setPageSize}
        >
          <Table size="small">
            <TableHead>
              <TableRow>
                {SORTABLE_COLUMNS.map(({key, label, align}) => (
                  <TableCell key={key} align={align ?? "left"}>
                    <TableSortLabel
                      active={sortBy === key}
                      direction={sortBy === key ? sortOrder : "asc"}
                      onClick={() => handleSortClick(key)}
                    >
                      {label}
                    </TableSortLabel>
                  </TableCell>
                ))}
                <TableCell align="right">Порог</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {isLoading ? (
                <TableRowLoader colSpan={COLUMN_COUNT} />
              ) : (data?.items.items.length ?? 0) === 0 ? (
                <TableRowEmpty colSpan={COLUMN_COUNT} message={emptyMessage} />
              ) : (
                data?.items.items.map((row) => (
                  <TableRow
                    key={row.catalogItemId}
                    sx={{
                      opacity: isFetching && !isLoading ? 0.5 : 1,
                      transition: "opacity 0.2s",
                    }}
                  >
                    <TableCell sx={{width: 110}}>
                      <CatalogItemTypeChip type={row.catalogItem.type} />
                    </TableCell>
                    <TableCell>
                      <CatalogItemLink catalogItemId={row.catalogItemId} onOpen={openCatalogDrawer}>
                        <Typography variant="body2">{row.catalogItem.fullName}</Typography>
                        {row.catalogItem.isArchived && (
                          <ArchiveIcon sx={{fontSize: 14, color: "warning.main", flexShrink: 0}} />
                        )}
                      </CatalogItemLink>
                    </TableCell>
                    <TableCell>{row.catalogItem.article}</TableCell>
                    <TableCell align="right">
                      <Typography variant="body2" sx={{fontWeight: 500}}>
                        {row.stock}
                      </Typography>
                    </TableCell>
                    <TableCell align="right">{row.dailyConsumption}</TableCell>
                    <TableCell align="right">
                      <StockForecastChip forecast={row} />
                    </TableCell>
                    <TableCell align="right">
                      <Stack
                        direction="row"
                        spacing={0.5}
                        sx={{alignItems: "center", justifyContent: "flex-end"}}
                      >
                        <Typography
                          variant="body2"
                          sx={{fontWeight: row.isWarningOverridden ? 600 : 400}}
                        >
                          {row.warningDays} дн.
                        </Typography>
                        {row.isWarningOverridden && (
                          <Tooltip title="Порог задан для этой позиции">
                            <PushPinIcon sx={{fontSize: 14, color: "info.main"}} />
                          </Tooltip>
                        )}
                        {canEditWarehouse && (
                          <Tooltip title="Изменить порог">
                            <IconButton
                              size="small"
                              onClick={() =>
                                setOverrideTarget({
                                  catalogItemId: row.catalogItemId,
                                  itemName: row.catalogItem.fullName,
                                  warningDays: row.warningDays,
                                  isWarningOverridden: row.isWarningOverridden,
                                })
                              }
                            >
                              <EditIcon fontSize="small" />
                            </IconButton>
                          </Tooltip>
                        )}
                      </Stack>
                    </TableCell>
                  </TableRow>
                ))
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

      {effectiveWarehouseId !== null && (
        <>
          <StockForecastSettingsDialog
            open={settingsOpen}
            warehouseId={effectiveWarehouseId}
            onClose={() => setSettingsOpen(false)}
          />
          <StockWarningOverrideDialog
            target={overrideTarget}
            warehouseId={effectiveWarehouseId}
            warehouseWarningDays={data?.warehouseWarningDays ?? null}
            onClose={() => setOverrideTarget(null)}
          />
        </>
      )}
    </>
  );
}

export default ForecastBasePage;
