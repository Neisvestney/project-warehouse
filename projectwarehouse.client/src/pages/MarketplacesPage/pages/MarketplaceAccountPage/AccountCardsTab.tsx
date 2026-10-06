import {useState} from "react";
import {
  Button,
  Chip,
  FormControlLabel,
  MenuItem,
  Select,
  Stack,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TableSortLabel,
  Typography,
} from "@mui/material";
import AutoFixHighIcon from "@mui/icons-material/AutoFixHigh";
import SyncAltIcon from "@mui/icons-material/SyncAlt";
import {useQuery, useQueryClient} from "@tanstack/react-query";
import {
  marketplacesGetAccountQueryKey,
  marketplacesGetCardsOptions,
} from "@/api/@tanstack/react-query.gen";
import {usePaginatedParams} from "@/hooks/usePaginatedParams";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {useTableSort} from "@/hooks/useTableSort";
import {useHasPermission} from "@/hooks/usePermission";
import FiltersBar from "@/components/FiltersBar";
import CopyableText from "@/components/CopyableText";
import DataTableContainer from "@/components/DataTableContainer";
import TableRowLoader from "@/components/TableRowLoader";
import TableRowEmpty from "@/components/TableRowEmpty";
import {CatalogItemLink} from "@/components/catalog/CatalogItemLink";
import SearchWithItemsInput from "@/components/catalog/SearchWithItemsInput";
import {useOpenCatalogItem} from "@/components/catalog/CatalogItemDrawerContext";
import CardImage from "@/components/marketplace/CardImage.tsx";
import CardMappingChip from "../../components/CardMappingChip";
import AutoMapCardsDialog from "../../components/AutoMapCardsDialog";
import RebindExternalOrdersDialog from "../../components/RebindExternalOrdersDialog";
import CardMappingDialog from "./CardMappingDialog";
import {
  ALL_MAPPING_STATES,
  MAPPING_STATE_LABELS,
  formatPrice,
} from "@/components/marketplace/marketplaceUtils.ts";
import type {
  MarketplaceCardDto,
  MarketplaceCardMappingState,
  MarketplaceCardSortBy,
} from "@/api/types.gen";

const SORT_COLUMNS: {key: MarketplaceCardSortBy; label: string}[] = [
  {key: "name", label: "Название"},
  {key: "offerId", label: "Артикул"},
  {key: "price", label: "Цена"},
  {key: "syncedAt", label: "Обновлена"},
];

interface AccountCardsTabProps {
  accountId: string;
}

function AccountCardsTab({accountId}: AccountCardsTabProps) {
  const queryClient = useQueryClient();
  const canMap = useHasPermission("integrations.map");
  const openCatalogItem = useOpenCatalogItem();

  const [editingCard, setEditingCard] = useState<MarketplaceCardDto | null>(null);
  const [isAutoMapOpen, setAutoMapOpen] = useState(false);
  const [isRebindOpen, setRebindOpen] = useState(false);

  const [searchString, setSearchString] = useSyncedWithQueryState(
    "search",
    (q) => (typeof q === "string" ? q : ""),
    (v) => v || null,
  );

  const [mappingState, setMappingState] = useSyncedWithQueryState<MarketplaceCardMappingState>(
    "mappingState",
    (q) =>
      ALL_MAPPING_STATES.includes(q as MarketplaceCardMappingState)
        ? (q as MarketplaceCardMappingState)
        : "all",
    (v) => (v === "all" ? null : v),
  );

  const [includeArchived, setIncludeArchived] = useSyncedWithQueryState(
    "archived",
    (q) => q === "true",
    (v) => (v ? "true" : null),
  );

  const [catalogItemIds, setCatalogItemIds] = useSyncedWithQueryState<string[]>(
    "item",
    (q) => (typeof q === "string" && q ? q.split(",").filter(Boolean) : []),
    (v) => v.join(",") || null,
  );

  const {sortBy, sortOrder, handleSortClick} = useTableSort(SORT_COLUMNS, "name");

  // searchString is already debounced by the search field, so it goes with the immediate params
  const {fetchParams, page, setPage, pageSize, setPageSize} = usePaginatedParams(
    {},
    [],
    {
      searchString: searchString || undefined,
      catalogItemIds: catalogItemIds.length > 0 ? catalogItemIds : undefined,
      mappingState,
      includeArchived,
      sortBy,
      sortOrder,
    },
    [searchString, catalogItemIds, mappingState, includeArchived, sortBy, sortOrder],
    {defaultPageSize: 50},
  );

  const listQueryOptions = marketplacesGetCardsOptions({
    path: {id: accountId},
    query: fetchParams,
  });
  const {data, isLoading, isFetching, dataUpdatedAt} = useQuery(listQueryOptions);

  const invalidate = async () => {
    await queryClient.invalidateQueries({queryKey: listQueryOptions.queryKey});
    await queryClient.invalidateQueries({
      queryKey: marketplacesGetAccountQueryKey({path: {id: accountId}}),
    });
  };

  return (
    <Stack spacing={2}>
      <FiltersBar
        activeCount={
          [searchString, catalogItemIds.length > 0, mappingState !== "all", includeArchived].filter(
            Boolean,
          ).length
        }
        actions={
          canMap ? (
            <>
              <Button
                variant="outlined"
                size="small"
                startIcon={<AutoFixHighIcon />}
                onClick={() => setAutoMapOpen(true)}
              >
                Сопоставить автоматически
              </Button>
              <Button
                variant="outlined"
                size="small"
                startIcon={<SyncAltIcon />}
                onClick={() => setRebindOpen(true)}
              >
                Перепривязать внешние заказы
              </Button>
            </>
          ) : null
        }
      >
        <SearchWithItemsInput
          text={searchString}
          onTextChange={setSearchString}
          itemIds={catalogItemIds}
          onItemIdsChange={setCatalogItemIds}
          sx={{minWidth: 280, flexGrow: 1}}
        />
        <Select
          value={mappingState}
          onChange={(e) => setMappingState(e.target.value as MarketplaceCardMappingState)}
          size="small"
          sx={{minWidth: 220}}
        >
          {ALL_MAPPING_STATES.map((s) => (
            <MenuItem key={s} value={s}>
              {MAPPING_STATE_LABELS[s]}
            </MenuItem>
          ))}
        </Select>
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
              <TableCell />
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
              <TableCell>SKU</TableCell>
              <TableCell>Позиция каталога</TableCell>
              <TableCell>Привязка</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {isLoading ? (
              <TableRowLoader colSpan={8} />
            ) : data?.items.length === 0 ? (
              <TableRowEmpty colSpan={8} message="Карточки не найдены" />
            ) : (
              data?.items.map((card) => (
                <TableRow
                  key={card.id}
                  hover
                  sx={{
                    cursor: canMap ? "pointer" : "default",
                    opacity: isFetching && !isLoading ? 0.5 : 1,
                    transition: "opacity 0.2s",
                  }}
                  onClick={() => canMap && setEditingCard(card)}
                >
                  <TableCell sx={{width: 56}}>
                    <CardImage src={card.primaryImageUrl} name={card.name} />
                  </TableCell>
                  <TableCell>
                    <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
                      <span>{card.name}</span>
                      {card.isArchived && <Chip label="Архивная" size="small" variant="outlined" />}
                      {card.isMarkedArchived && (
                        <Chip
                          label="Не используется"
                          size="small"
                          variant="outlined"
                          color="warning"
                        />
                      )}
                    </Stack>
                  </TableCell>
                  <TableCell sx={{fontFamily: "monospace"}}>
                    <CopyableText value={card.offerId} />
                  </TableCell>
                  <TableCell>{formatPrice(card.price, card.currencyCode)}</TableCell>
                  <TableCell>{new Date(card.syncedAt).toLocaleDateString("ru-RU")}</TableCell>
                  <TableCell sx={{fontFamily: "monospace"}}>{card.sku ?? "—"}</TableCell>
                  <TableCell>
                    {card.catalogItemId ? (
                      <CatalogItemLink catalogItemId={card.catalogItemId} onOpen={openCatalogItem}>
                        <Stack>
                          <Typography variant="body2">{card.catalogItemFullName}</Typography>
                          <Typography variant="caption" color="text.secondary">
                            {card.catalogItemArticle}
                          </Typography>
                        </Stack>
                      </CatalogItemLink>
                    ) : (
                      "—"
                    )}
                  </TableCell>
                  <TableCell>
                    <CardMappingChip card={card} />
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </DataTableContainer>

      <CardMappingDialog
        card={editingCard}
        onClose={() => setEditingCard(null)}
        onSaved={invalidate}
        dataUpdatedAt={dataUpdatedAt}
      />

      <AutoMapCardsDialog
        open={isAutoMapOpen}
        onClose={() => setAutoMapOpen(false)}
        accountId={accountId}
      />

      <RebindExternalOrdersDialog
        open={isRebindOpen}
        onClose={() => setRebindOpen(false)}
        accountId={accountId}
      />
    </Stack>
  );
}

export default AccountCardsTab;
