import {useState} from "react";
import {
  Box,
  Button,
  Checkbox,
  Chip,
  IconButton,
  ListItemText,
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
  FormControl,
  Select,
  InputLabel,
  MenuItem,
} from "@mui/material";
import ArchiveIcon from "@mui/icons-material/Archive";
import ImageOutlinedIcon from "@mui/icons-material/ImageOutlined";
import FileImage from "@/components/files/FileImage";
import RefreshIcon from "@mui/icons-material/Refresh";
import StarIcon from "@mui/icons-material/Star";
import {useQuery} from "@tanstack/react-query";
import {catalogGetAllOptions} from "@/api/@tanstack/react-query.gen";
import {type CatalogItemType, type CatalogSortBy} from "@/api/types.gen";
import {CATALOG_ITEM_TYPE_CONFIG, CATALOG_ITEM_TYPES} from "@/features/catalog";
import {useDebouncedSyncedWithQueryState} from "@/hooks/useDebouncedSyncedWithQueryState";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {usePaginatedParams} from "@/hooks/usePaginatedParams";
import {useTableSort} from "@/hooks/useTableSort";
import PageGenericHeader from "@/components/PageGenericHeader.tsx";
import AppBreadcrumbs from "@/components/AppBreadcrumbs.tsx";
import SearchInput from "@/components/SearchInput.tsx";
import DataTableContainer from "@/components/DataTableContainer.tsx";
import TableRowLoader from "@/components/TableRowLoader.tsx";
import TableRowEmpty from "@/components/TableRowEmpty.tsx";
import {CatalogItemDrawer} from "@/components/catalog/CatalogItemDrawer";
import {CreateCatalogItemDialog} from "@/components/catalog/CreateCatalogItemDialog";
import CatalogItemTypeChip from "@/components/catalog/CatalogItemTypeChip";
import {CatalogItemLink} from "@/components/catalog/CatalogItemLink";
import {useDrawerSearchParamsState} from "@/hooks/useDrawerSearchParamsState.ts";
import {useHasPermission} from "@/hooks/usePermission";
import AddIcon from "@mui/icons-material/Add";
import FiltersBar from "@/components/FiltersBar.tsx";
import {NOUNS, pluralCount} from "@/utils/pluralUtils";

const DEFAULT_ITEM_TYPES = CATALOG_ITEM_TYPES;

const SORTABLE_COLUMNS: {key: CatalogSortBy; label: string}[] = [
  {key: "type", label: "Тип"},
  {key: "name", label: "Название"},
  {key: "article", label: "Артикул"},
  {key: "barcode", label: "Штрихкод"},
];

function CatalogPage() {
  const [selectedItemId, openDrawer, closeDrawer] = useDrawerSearchParamsState("item");
  const [createOpen, setCreateOpen] = useState(false);
  const canEdit = useHasPermission("catalog.edit");

  const [inputValue, setInputValue, searchString] = useDebouncedSyncedWithQueryState(
    "search",
    (q) => (typeof q === "string" ? q : ""),
    (v) => v || null,
  );

  const {sortBy, sortOrder, handleSortClick} = useTableSort(SORTABLE_COLUMNS, "name");

  const [itemTypes, setItemTypes] = useSyncedWithQueryState<CatalogItemType[]>(
    "types",
    (q) => {
      if (!q) return DEFAULT_ITEM_TYPES;
      const parsed = q
        .split(",")
        .filter((p) => CATALOG_ITEM_TYPES.includes(p as CatalogItemType)) as CatalogItemType[];
      return parsed.length > 0 ? parsed : DEFAULT_ITEM_TYPES;
    },
    (v) => {
      const isDefault =
        v.length === DEFAULT_ITEM_TYPES.length && DEFAULT_ITEM_TYPES.every((t) => v.includes(t));
      return isDefault ? null : v.join(",") || null;
    },
  );

  const [isArchived, setIsArchived] = useSyncedWithQueryState<boolean | null>(
    "archived",
    (q) => (q === "true" ? true : q === "false" ? false : q === "null" ? null : false),
    (v) => (v === false ? null : String(v)),
  );

  const {fetchParams, page, setPage, pageSize, setPageSize} = usePaginatedParams(
    {},
    [],
    {
      searchString,
      sortBy,
      sortOrder,
      itemTypes: itemTypes.length < CATALOG_ITEM_TYPES.length ? itemTypes : undefined,
      isArchived: isArchived ?? undefined,
    },
    [searchString, sortBy, sortOrder, itemTypes, isArchived],
  );

  const {data, isLoading, isFetching, refetch} = useQuery(
    catalogGetAllOptions({query: fetchParams}),
  );

  return (
    <>
      <Stack spacing={2}>
        <AppBreadcrumbs path={[{name: "Каталог", link: "/catalog"}, {name: "Список"}]} />
        <PageGenericHeader
          title="Каталог"
          right={
            <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
              <IconButton color="inherit" onClick={() => refetch()}>
                <RefreshIcon />
              </IconButton>
              {canEdit && (
                <Button
                  endIcon={<AddIcon />}
                  variant="outlined"
                  size="small"
                  onClick={() => setCreateOpen(true)}
                >
                  Создать
                </Button>
              )}
            </Stack>
          }
        >
          <SearchInput value={inputValue} onChange={setInputValue} />
        </PageGenericHeader>
        <FiltersBar>
          <FormControl size="small" sx={{minWidth: 150}}>
            <InputLabel>Тип</InputLabel>
            <Select
              multiple
              label="Тип"
              value={itemTypes}
              onChange={(e) => setItemTypes(e.target.value as CatalogItemType[])}
              renderValue={(selected) => {
                if (selected.length === CATALOG_ITEM_TYPES.length) return "Все";
                if (selected.length === 0) return "Нет";
                if (selected.length === 1) return CATALOG_ITEM_TYPE_CONFIG[selected[0]].label;
                return pluralCount(selected.length, NOUNS.itemType);
              }}
            >
              {CATALOG_ITEM_TYPES.map((type) => (
                <MenuItem key={type} value={type}>
                  <Checkbox checked={itemTypes.includes(type)} size="small" />
                  <ListItemText primary={CATALOG_ITEM_TYPE_CONFIG[type].label} />
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
                <TableCell sx={{width: 56}} />
                {SORTABLE_COLUMNS.map(({key, label}) => (
                  <TableCell key={key}>
                    <TableSortLabel
                      active={sortBy === key}
                      direction={sortBy === key ? sortOrder : "asc"}
                      onClick={() => handleSortClick(key)}
                    >
                      {label}
                    </TableSortLabel>
                  </TableCell>
                ))}
                <TableCell>Теги</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {isLoading ? (
                <TableRowLoader colSpan={6} />
              ) : data?.items.length === 0 ? (
                <TableRowEmpty colSpan={6} message="Позиции не найдены" />
              ) : (
                data?.items.map((item) => (
                  <TableRow
                    key={item.id}
                    hover
                    selected={item.id === selectedItemId}
                    sx={{
                      cursor: "pointer",
                      opacity: isFetching && !isLoading ? 0.5 : 1,
                      transition: "opacity 0.2s",
                    }}
                    onClick={() => openDrawer(item.id)}
                  >
                    <TableCell sx={{width: 56}}>
                      <Box
                        sx={{
                          width: 40,
                          height: 40,
                          borderRadius: 1,
                          overflow: "hidden",
                          bgcolor: "action.hover",
                        }}
                      >
                        <FileImage
                          source={item.mainImage}
                          previewWidth={64}
                          style={{height: "100%"}}
                          fallback={
                            <Box
                              sx={{
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "center",
                                width: "100%",
                                height: "100%",
                              }}
                            >
                              <ImageOutlinedIcon fontSize="small" color="disabled" />
                            </Box>
                          }
                        />
                      </Box>
                    </TableCell>
                    <TableCell sx={{width: 120}}>
                      <CatalogItemTypeChip type={item.type} />
                    </TableCell>
                    <TableCell>
                      <CatalogItemLink catalogItemId={item.id} onOpen={openDrawer} spacing={0.5}>
                        <Typography variant="body2">{item.fullName}</Typography>
                        {item.isArchived && (
                          <ArchiveIcon sx={{fontSize: 14, color: "warning.main", flexShrink: 0}} />
                        )}
                      </CatalogItemLink>
                    </TableCell>
                    <TableCell>{item.article}</TableCell>
                    <TableCell>{item.barcode ?? "—"}</TableCell>
                    <TableCell>
                      {item.tags.length > 0 && (
                        <Stack direction="row" spacing={0.5} sx={{flexWrap: "wrap", gap: 0.5}}>
                          {item.tags.map((tag) => (
                            <Chip key={tag.id} label={tag.name} size="small" variant="outlined" />
                          ))}
                        </Stack>
                      )}
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </DataTableContainer>
      </Stack>

      <CatalogItemDrawer itemId={selectedItemId} onClose={closeDrawer} onOpenItem={openDrawer} />

      <CreateCatalogItemDialog
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onCreated={(id) => {
          setCreateOpen(false);
          openDrawer(id);
        }}
      />
    </>
  );
}

export default CatalogPage;
