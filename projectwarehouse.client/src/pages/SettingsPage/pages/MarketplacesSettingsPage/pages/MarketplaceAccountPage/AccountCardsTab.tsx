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
import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";
import {useSnackbar} from "notistack";
import {
  marketplacesAutoMapCardsMutation,
  marketplacesGetAccountQueryKey,
  marketplacesGetCardsOptions,
} from "@/api/@tanstack/react-query.gen";
import {extractErrorMessage} from "@/utils/errorUtils";
import {useDebouncedSyncedWithQueryState} from "@/hooks/useDebouncedSyncedWithQueryState";
import {usePaginatedParams} from "@/hooks/usePaginatedParams";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {useTableSort} from "@/hooks/useTableSort";
import {useHasPermission} from "@/hooks/usePermission";
import FiltersBar from "@/components/FiltersBar";
import SearchInput from "@/components/SearchInput";
import DataTableContainer from "@/components/DataTableContainer";
import TableRowLoader from "@/components/TableRowLoader";
import TableRowEmpty from "@/components/TableRowEmpty";
import {CatalogItemLink} from "@/components/catalog/CatalogItemLink";
import {useOpenCatalogItem} from "@/components/catalog/CatalogItemDrawerContext";
import CardImage from "../../components/CardImage";
import CardMappingChip from "../../components/CardMappingChip";
import CardMappingDialog from "./CardMappingDialog";
import {ALL_MAPPING_STATES, MAPPING_STATE_LABELS, formatPrice} from "../../marketplaceUtils";
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
  const {enqueueSnackbar} = useSnackbar();
  const canMap = useHasPermission("integrations.map");
  const openCatalogItem = useOpenCatalogItem();

  const [editingCard, setEditingCard] = useState<MarketplaceCardDto | null>(null);

  const [inputValue, setInputValue, searchString] = useDebouncedSyncedWithQueryState(
    "search",
    (q) => (typeof q === "string" ? q : ""),
    (v) => v || null,
  );

  // Рабочий список — несопоставленные, поэтому это и есть значение по умолчанию
  const [mappingState, setMappingState] = useSyncedWithQueryState<MarketplaceCardMappingState>(
    "mappingState",
    (q) =>
      ALL_MAPPING_STATES.includes(q as MarketplaceCardMappingState)
        ? (q as MarketplaceCardMappingState)
        : "unmapped",
    (v) => (v === "unmapped" ? null : v),
  );

  const [includeArchived, setIncludeArchived] = useSyncedWithQueryState(
    "archived",
    (q) => q === "true",
    (v) => (v ? "true" : null),
  );

  const [catalogItemIdFilter, setCatalogItemIdFilter] = useSyncedWithQueryState(
      "catalogItemId",
      (q) => (typeof q === "string" ? q : null),
      (v) => v || null,
  );

  const {sortBy, sortOrder, handleSortClick} = useTableSort(SORT_COLUMNS, "name");

  const {fetchParams, page, setPage, pageSize, setPageSize} = usePaginatedParams(
    {searchString: searchString || undefined, catalogItemId: catalogItemIdFilter || undefined},
    [searchString],
    {mappingState, includeArchived, sortBy, sortOrder},
    [mappingState, includeArchived, sortBy, sortOrder],
    {defaultPageSize: 50},
  );

  const listQueryOptions = marketplacesGetCardsOptions({
    path: {id: accountId},
    query: fetchParams,
  });
  const {data, isLoading, isFetching} = useQuery(listQueryOptions);

  const invalidate = async () => {
    await queryClient.invalidateQueries({queryKey: listQueryOptions.queryKey});
    await queryClient.invalidateQueries({
      queryKey: marketplacesGetAccountQueryKey({path: {id: accountId}}),
    });
  };

  const autoMapMutation = useMutation({
    ...marketplacesAutoMapCardsMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: async (result) => {
      enqueueSnackbar(
        `Сопоставлено ${result.mapped}, требует ручного разбора ${result.remaining}`,
        {variant: "success"},
      );
      await invalidate();
    },
    onError: (err) =>
      enqueueSnackbar(extractErrorMessage(err) || "Не удалось выполнить автосопоставление", {
        variant: "error",
      }),
  });

  return (
    <Stack spacing={2}>
      <FiltersBar>
        <SearchInput value={inputValue} onChange={setInputValue} size="small" />
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
        {canMap && (
          <Button
            variant="outlined"
            size="small"
            startIcon={<AutoFixHighIcon />}
            disabled={autoMapMutation.isPending}
            onClick={() => autoMapMutation.mutate({path: {id: accountId}})}
          >
            Сопоставить автоматически
          </Button>
        )}
      </FiltersBar>
      {catalogItemIdFilter && (
          <Stack spacing={1} direction={"row"}>
            <Chip color={"info"} label={"Применен фильтр по позиции каталога"} onDelete={() => setCatalogItemIdFilter(null)}/>
          </Stack>
      )}
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
                    </Stack>
                  </TableCell>
                  <TableCell sx={{fontFamily: "monospace"}}>{card.offerId}</TableCell>
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
      />
    </Stack>
  );
}

export default AccountCardsTab;
