import {
  Button,
  Chip,
  IconButton,
  MenuItem,
  Select,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TableSortLabel,
  Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import RuleIcon from "@mui/icons-material/Rule";
import RefreshIcon from "@mui/icons-material/Refresh";
import {useQuery} from "@tanstack/react-query";
import {Link as RouterLink} from "react-router";
import {marketplacesGetAccountsOptions} from "@/api/@tanstack/react-query.gen";
import {useDebouncedSyncedWithQueryState} from "@/hooks/useDebouncedSyncedWithQueryState";
import {usePaginatedParams} from "@/hooks/usePaginatedParams";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {useTableSort} from "@/hooks/useTableSort";
import {useHasPermission} from "@/hooks/usePermission";
import PageGenericHeader from "@/components/PageGenericHeader";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import SearchInput from "@/components/SearchInput";
import FiltersBar from "@/components/FiltersBar";
import DataTableContainer from "@/components/DataTableContainer";
import TableRowLoader from "@/components/TableRowLoader";
import TableRowEmpty from "@/components/TableRowEmpty";
import LinkTableRow from "@/components/LinkTableRow";
import MarketplaceStatusChip from "./components/MarketplaceStatusChip";
import {MARKETPLACE_TYPE_LABELS, formatDateTime, MARKETPLACE_TYPE_COLORS} from "./marketplaceUtils";
import type {MarketplaceAccountSortBy, MarketplaceType} from "@/api/types.gen";

const SORT_COLUMNS: {key: MarketplaceAccountSortBy; label: string}[] = [
  {key: "name", label: "Магазин"},
  {key: "createdAt", label: "Подключён"},
  {key: "lastSyncAt", label: "Синхронизация"},
];

const ALL_TYPES: MarketplaceType[] = ["ozon", "wildberries"];

function MarketplacesSettingsPage() {
  const canEdit = useHasPermission("integrations.edit");

  const [inputValue, setInputValue, searchString] = useDebouncedSyncedWithQueryState(
    "search",
    (q) => (typeof q === "string" ? q : ""),
    (v) => v || null,
  );

  const [type, setType] = useSyncedWithQueryState<MarketplaceType | "">(
    "type",
    (q) => (ALL_TYPES.includes(q as MarketplaceType) ? (q as MarketplaceType) : ""),
    (v) => v || null,
  );

  const [isActive, setIsActive] = useSyncedWithQueryState<"" | "true" | "false">(
    "active",
    (q) => (q === "true" || q === "false" ? q : ""),
    (v) => v || null,
  );

  const {sortBy, sortOrder, handleSortClick} = useTableSort(SORT_COLUMNS, "name");

  const {fetchParams, page, setPage, pageSize, setPageSize} = usePaginatedParams(
    {searchString: searchString || undefined},
    [searchString],
    {
      type: (type as MarketplaceType) || undefined,
      isActive: isActive === "" ? undefined : isActive === "true",
      sortBy,
      sortOrder,
    },
    [type, isActive, sortBy, sortOrder],
  );

  const {data, isLoading, isFetching, refetch} = useQuery(
    marketplacesGetAccountsOptions({query: fetchParams}),
  );

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs
        path={[{name: "Маркетплейсы", link: "/settings/integrations"}, {name: "Магазины"}]}
      />
      <PageGenericHeader
        title="Маркетплейсы"
        refresh={
          <IconButton color="inherit" onClick={() => refetch()}>
            <RefreshIcon />
          </IconButton>
        }
        actions={
          <>
            <Button
              variant="text"
              startIcon={<RuleIcon />}
              size="small"
              component={RouterLink}
              to="/settings/integrations/auto-map-rules"
            >
              Правила автосопоставления
            </Button>
            {canEdit && (
              <Button
                variant="outlined"
                endIcon={<AddIcon />}
                size="small"
                component={RouterLink}
                to="/settings/integrations/new"
              >
                Подключить магазин
              </Button>
            )}
          </>
        }
      >
        <SearchInput value={inputValue} onChange={setInputValue} />
      </PageGenericHeader>
      <FiltersBar>
        <Select
          value={type}
          onChange={(e) => setType(e.target.value as MarketplaceType | "")}
          size="small"
          displayEmpty
          sx={{minWidth: 160}}
        >
          <MenuItem value="">Все площадки</MenuItem>
          {ALL_TYPES.map((t) => (
            <MenuItem key={t} value={t}>
              {MARKETPLACE_TYPE_LABELS[t]}
            </MenuItem>
          ))}
        </Select>
        <Select
          value={isActive}
          onChange={(e) => setIsActive(e.target.value as "" | "true" | "false")}
          size="small"
          displayEmpty
          sx={{minWidth: 160}}
        >
          <MenuItem value="">Все аккаунты</MenuItem>
          <MenuItem value="true">Активные</MenuItem>
          <MenuItem value="false">Отключённые</MenuItem>
        </Select>
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
              <TableCell sortDirection={sortBy === "name" ? sortOrder : false}>
                <TableSortLabel
                  active={sortBy === "name"}
                  direction={sortBy === "name" ? sortOrder : "asc"}
                  onClick={() => handleSortClick("name")}
                >
                  Магазин
                </TableSortLabel>
              </TableCell>
              <TableCell>Площадка</TableCell>
              <TableCell>Статус</TableCell>
              <TableCell sortDirection={sortBy === "lastSyncAt" ? sortOrder : false}>
                <TableSortLabel
                  active={sortBy === "lastSyncAt"}
                  direction={sortBy === "lastSyncAt" ? sortOrder : "asc"}
                  onClick={() => handleSortClick("lastSyncAt")}
                >
                  Синхронизация
                </TableSortLabel>
              </TableCell>
              <TableCell>Складов</TableCell>
              <TableCell>Карточек</TableCell>
              <TableCell>Не сопоставлено</TableCell>
              <TableCell>Активен</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {isLoading ? (
              <TableRowLoader colSpan={8} />
            ) : data?.items.length === 0 ? (
              <TableRowEmpty colSpan={8} message="Магазины не подключены" />
            ) : (
              data?.items.map((account) => (
                <LinkTableRow
                  key={account.id}
                  to={`/settings/integrations/${account.id}`}
                  ariaLabel={`Магазин ${account.name}`}
                  sx={{
                    opacity: isFetching && !isLoading ? 0.5 : 1,
                    transition: "opacity 0.2s",
                  }}
                >
                  <TableCell>{account.name}</TableCell>
                  <TableCell>
                    <Chip
                      size={"small"}
                      label={MARKETPLACE_TYPE_LABELS[account.type]}
                      color={MARKETPLACE_TYPE_COLORS[account.type]}
                    />
                  </TableCell>
                  <TableCell>
                    <MarketplaceStatusChip status={account.lastSyncStatus} />
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2" color="text.secondary">
                      {formatDateTime(account.lastSyncAt)}
                    </Typography>
                  </TableCell>
                  <TableCell>{account.warehouseCount}</TableCell>
                  <TableCell>{account.cardCount}</TableCell>
                  <TableCell>
                    {account.unmappedCardCount > 0 ? (
                      <Chip label={account.unmappedCardCount} color="warning" size="small" />
                    ) : (
                      "—"
                    )}
                  </TableCell>
                  <TableCell>
                    {account.isActive ? (
                      <Chip label="Да" size="small" />
                    ) : (
                      <Chip label="Нет" color="default" variant="outlined" size="small" />
                    )}
                  </TableCell>
                </LinkTableRow>
              ))
            )}
          </TableBody>
        </Table>
      </DataTableContainer>
    </Stack>
  );
}

export default MarketplacesSettingsPage;
