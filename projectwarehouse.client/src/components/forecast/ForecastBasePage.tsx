import {Fragment, useState} from "react";
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
  Checkbox,
  FormControlLabel,
} from "@mui/material";
import ArchiveIcon from "@mui/icons-material/Archive";
import Inventory2Icon from "@mui/icons-material/Inventory2";
import LayersIcon from "@mui/icons-material/Layers";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import StarIcon from "@mui/icons-material/Star";
import RefreshIcon from "@mui/icons-material/Refresh";
import SettingsIcon from "@mui/icons-material/Settings";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import {useQuery} from "@tanstack/react-query";
import {stockForecastGetListOptions} from "@/api/@tanstack/react-query.gen";
import type {StockForecastRowDto, StockForecastSortBy} from "@/api/types.gen";
import PageGenericHeader from "@/components/PageGenericHeader";
import DataTableContainer from "@/components/DataTableContainer";
import FiltersBar from "@/components/FiltersBar";
import SearchInput from "@/components/SearchInput";
import TableRowEmpty from "@/components/TableRowEmpty";
import TableRowLoader from "@/components/TableRowLoader";
import WarehousesSelect from "@/components/WarehousesSelect";
import CatalogTagsFilter from "@/components/catalog/CatalogTagsFilter";
import CatalogTypesFilter from "@/components/catalog/CatalogTypesFilter";
import {CatalogItemDrawer} from "@/components/catalog/CatalogItemDrawer";
import {PHYSICAL_CATALOG_ITEMS, useCatalogTypesFilter} from "@/features/catalog";
import {useDebouncedSyncedWithQueryState} from "@/hooks/useDebouncedSyncedWithQueryState";
import {useDrawerSearchParamsState} from "@/hooks/useDrawerSearchParamsState";
import {usePaginatedParams} from "@/hooks/usePaginatedParams";
import {useHasPermission} from "@/hooks/usePermission";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {useTableSort} from "@/hooks/useTableSort";
import ForecastTableRow from "./ForecastTableRow";
import StockForecastSettingsDialog from "./StockForecastSettingsDialog";
import StockWarningOverrideDialog, {
  type StockWarningOverrideTarget,
} from "./StockWarningOverrideDialog";

const COLUMN_COUNT = 9;

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
    (q) => (q === "true" ? true : q === "null" ? null : false),
    (v) => (v === false ? null : String(v)),
  );

  const [isVariation, setIsVariation] = useSyncedWithQueryState<boolean | null>(
    "variations",
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

  const effectiveWarehouseId = warehouseId ?? filterWarehouseId;

  // The type filter narrows physical rows only, so an empty selection leaves just the variations.
  const effectiveIsVariation = itemTypes.length === 0 && isVariation === null ? true : isVariation;

  const {fetchParams, page, setPage, pageSize, setPageSize} = usePaginatedParams(
    {},
    [],
    {
      WarehouseId: effectiveWarehouseId ?? undefined,
      SearchString: searchString || undefined,
      CatalogItemTypes: itemTypes.length < PHYSICAL_CATALOG_ITEMS.length ? itemTypes : undefined,
      TagIds: tagIds.length > 0 ? tagIds : undefined,
      IsArchived: isArchived ?? undefined,
      IsVariation: effectiveIsVariation ?? undefined,
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
      effectiveIsVariation,
      onlyWarnings,
      accountForAssembly,
      sortBy,
      sortOrder,
    ],
  );

  // Empty type selection can't be expressed server-side (no types == no filter), so match nothing here
  const noItemTypes = itemTypes.length === 0 && isVariation === false;

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
  const hasVariationRows = data?.items.items.some((row) => row.members) ?? false;

  const [catalogItemId, openCatalogDrawer, closeCatalogDrawer] =
    useDrawerSearchParamsState("catalogItem");

  const [settingsOpen, setSettingsOpen] = useState(false);
  const [overrideTarget, setOverrideTarget] = useState<StockWarningOverrideTarget | null>(null);
  const [expandedIds, setExpandedIds] = useState<ReadonlySet<string>>(new Set());

  const toggleExpanded = (id: string) =>
    setExpandedIds((prev) => {
      const next = new Set(prev);
      if (!next.delete(id)) next.add(id);
      return next;
    });

  const editThreshold = (row: StockForecastRowDto) =>
    setOverrideTarget({
      catalogItemId: row.catalogItemId,
      itemName: row.catalogItem.fullName,
      warningDays: row.warningDays,
      isWarningOverridden: row.isWarningOverridden,
    });

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

        <FiltersBar
          activeCount={
            [
              itemTypes.length < PHYSICAL_CATALOG_ITEMS.length,
              tagIds.length > 0,
              isArchived !== false,
              isVariation !== null,
              onlyWarnings,
            ].filter(Boolean).length
          }
        >
          {showWarehouseFilter && (
            <WarehousesSelect
              value={filterWarehouseId}
              onChange={setFilterWarehouseId}
              canAutoSelect
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

          <ToggleButtonGroup
            exclusive
            size="small"
            value={effectiveIsVariation}
            onChange={(_, v: boolean | null) => setIsVariation(v)}
          >
            <ToggleButton value={false} sx={{gap: 0.5}}>
              <Inventory2Icon fontSize="small" />
              Товары
            </ToggleButton>
            <ToggleButton value={true} sx={{gap: 0.5}}>
              <LayersIcon fontSize="small" />
              Вариации
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
          <Tooltip title="Вариация в заказе резервируется только в строке самой вариации: какой участник уйдёт, ещё не известно">
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
            <TableHead sx={{"& .MuiTableCell-root": {whiteSpace: "nowrap"}}}>
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
                <TableCell align="right">
                  <Tooltip title="Сколько дней назад остаток последний раз был нулевым в пределах окна расчёта">
                    <span>С последнего 0</span>
                  </Tooltip>
                </TableCell>
                <TableCell align="right">
                  <Tooltip title="Сколько дней окна расчёта товара не было на складе — эти дни не входят в расход/день">
                    <span>Без остатка</span>
                  </Tooltip>
                </TableCell>
                <TableCell align="right">
                  <Tooltip title="Порог предупреждения в днях">
                    <span>Порог</span>
                  </Tooltip>
                </TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {isLoading ? (
                <TableRowLoader colSpan={COLUMN_COUNT} />
              ) : (data?.items.items.length ?? 0) === 0 ? (
                <TableRowEmpty colSpan={COLUMN_COUNT} message={emptyMessage} />
              ) : (
                data?.items.items.map((row) => {
                  const expanded = expandedIds.has(row.catalogItemId);
                  const rowProps = {
                    windowDays: data.windowDays,
                    dimmed: isFetching && !isLoading,
                    canEditThreshold: canEditWarehouse,
                    onOpenItem: openCatalogDrawer,
                    onEditThreshold: editThreshold,
                    withExpandSlot: hasVariationRows,
                  };
                  return (
                    <Fragment key={row.catalogItemId}>
                      <ForecastTableRow
                        row={row}
                        {...rowProps}
                        expanded={expanded}
                        onToggleExpanded={
                          row.members ? () => toggleExpanded(row.catalogItemId) : undefined
                        }
                      />
                      {expanded &&
                        row.members?.map((member) => (
                          <ForecastTableRow
                            key={member.catalogItemId}
                            row={member}
                            {...rowProps}
                            isMember
                          />
                        ))}
                    </Fragment>
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
