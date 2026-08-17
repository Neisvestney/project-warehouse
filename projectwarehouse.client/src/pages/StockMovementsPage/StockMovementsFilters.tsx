import {useMemo, useState} from "react";
import {
  Button,
  Checkbox,
  FormControl,
  FormControlLabel,
  InputLabel,
  ListItemText,
  MenuItem,
  Select,
  TextField,
} from "@mui/material";
import {useQuery} from "@tanstack/react-query";
import {
  storagePlacesGetNodesOptions,
  usersGetAllOptions,
  warehousesGetAllOptions,
  warehousesGetByIdOptions,
} from "@/api/@tanstack/react-query.gen";
import type {CatalogItemSelectDto, StockMovementDirection} from "@/api/types.gen";
import CatalogItemsSelect from "@/components/CatalogItemsSelect";
import FiltersBar from "@/components/FiltersBar";
import LocalOfferIcon from "@mui/icons-material/LocalOffer";
import {buildNodePath, formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";
import {useHasPermission} from "@/hooks/usePermission";
import {PHYSICAL_CATALOG_ITEMS} from "@/features/catalog";
import {STOCK_MOVEMENT_ACTIONS, STOCK_MOVEMENT_DIRECTIONS} from "./stockMovementsConstants";
import type {useStockMovementsFilters} from "./useStockMovementsFilters";
import AddItemsByTagDialog from "./AddItemsByTagDialog";

type StockMovementsFiltersProps = ReturnType<typeof useStockMovementsFilters> & {
  /** Resolved DTOs for `filter.catalogItemIds`, in the same order. */
  items: CatalogItemSelectDto[];
};

function StockMovementsFilters({
  items,
  filter,
  showTransfers,
  setShowTransfers,
  setCatalogItemIds,
  setFrom,
  setTo,
  setWarehouseId,
  setStoragePlaceId,
  setNodeId,
  setUserId,
  setActions,
  setDirections,
}: StockMovementsFiltersProps) {
  const canViewUsers = useHasPermission("users.view");
  const [tagDialogOpen, setTagDialogOpen] = useState(false);

  const {data: warehouses} = useQuery(warehousesGetAllOptions({query: {pageSize: 200}}));

  const {data: warehouse} = useQuery({
    ...warehousesGetByIdOptions({path: {id: filter.warehouseId!}}),
    enabled: filter.warehouseId !== null,
  });

  const {data: nodes} = useQuery({
    ...storagePlacesGetNodesOptions({path: {id: filter.storagePlaceId!}}),
    enabled: filter.storagePlaceId !== null,
  });

  const {data: users} = useQuery({
    ...usersGetAllOptions({query: {pageSize: 200}}),
    enabled: canViewUsers,
  });

  // Node names repeat across parents, so the dropdown needs full paths
  const nodeOptions = useMemo(() => {
    const list = nodes ?? [];
    const placeName =
      warehouse?.storagePlaces.find((place) => place.id === filter.storagePlaceId)?.name ?? "";
    return list
      .map((node) => ({
        id: node.id,
        label: formatStoragePlaceNodeName(buildNodePath(list, node.id, placeName)),
      }))
      .sort((a, b) => a.label.localeCompare(b.label, "ru"));
  }, [nodes, warehouse, filter.storagePlaceId]);

  return (
    <FiltersBar sx={{alignItems: "flex-start"}}>
      <CatalogItemsSelect
        multiple
        fullWidth
        size="small"
        sx={{minWidth: 320, flexGrow: 1}}
        types={PHYSICAL_CATALOG_ITEMS}
        value={items}
        // At least one item must stay selected — it is what the columns are made of
        onChange={(items) => setCatalogItemIds(items.map((item) => item.id))}
        clearIcon={null}
      />

      <Button
        size="small"
        variant="outlined"
        startIcon={<LocalOfferIcon />}
        onClick={() => setTagDialogOpen(true)}
        sx={{flexShrink: 0, height: 40}}
      >
        По тегу
      </Button>

      <AddItemsByTagDialog
        open={tagDialogOpen}
        onClose={() => setTagDialogOpen(false)}
        types={PHYSICAL_CATALOG_ITEMS}
        onAdd={(added) =>
          setCatalogItemIds([
            ...filter.catalogItemIds,
            ...added.map((item) => item.id).filter((id) => !filter.catalogItemIds.includes(id)),
          ])
        }
      />

      <TextField
        size="small"
        type="date"
        label="С"
        value={filter.from ?? ""}
        onChange={(e) => setFrom(e.target.value || null)}
        slotProps={{inputLabel: {shrink: true}}}
        sx={{width: 165}}
      />
      <TextField
        size="small"
        type="date"
        label="По"
        value={filter.to ?? ""}
        onChange={(e) => setTo(e.target.value || null)}
        slotProps={{inputLabel: {shrink: true}}}
        sx={{width: 165}}
      />

      <FormControl size="small" sx={{minWidth: 180}}>
        <InputLabel>Склад</InputLabel>
        <Select
          label="Склад"
          value={filter.warehouseId ?? ""}
          onChange={(e) => setWarehouseId(e.target.value || null)}
        >
          <MenuItem value="">Все склады</MenuItem>
          {warehouses?.items.map((item) => (
            <MenuItem key={item.id} value={item.id}>
              {item.name}
            </MenuItem>
          ))}
        </Select>
      </FormControl>

      <FormControl size="small" sx={{minWidth: 180}} disabled={filter.warehouseId === null}>
        <InputLabel>Место хранения</InputLabel>
        <Select
          label="Место хранения"
          value={filter.storagePlaceId ?? ""}
          onChange={(e) => setStoragePlaceId(e.target.value || null)}
        >
          <MenuItem value="">Все места</MenuItem>
          {warehouse?.storagePlaces.map((place) => (
            <MenuItem key={place.id} value={place.id}>
              {place.name}
            </MenuItem>
          ))}
        </Select>
      </FormControl>

      <FormControl size="small" sx={{minWidth: 180}} disabled={filter.storagePlaceId === null}>
        <InputLabel>Ячейка</InputLabel>
        <Select
          label="Ячейка"
          value={filter.nodeId ?? ""}
          onChange={(e) => setNodeId(e.target.value || null)}
        >
          <MenuItem value="">Все ячейки</MenuItem>
          {nodeOptions.map((node) => (
            <MenuItem key={node.id} value={node.id}>
              {node.label}
            </MenuItem>
          ))}
        </Select>
      </FormControl>

      {canViewUsers && (
        <FormControl size="small" sx={{minWidth: 180}}>
          <InputLabel>Сотрудник</InputLabel>
          <Select
            label="Сотрудник"
            value={filter.userId ?? ""}
            onChange={(e) => setUserId(e.target.value || null)}
          >
            <MenuItem value="">Все сотрудники</MenuItem>
            {users?.items.map((user) => (
              <MenuItem key={user.id} value={user.id}>
                {user.username}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
      )}

      {/*<FormControl size="small" sx={{minWidth: 180}}>*/}
      {/*  <InputLabel>Направление</InputLabel>*/}
      {/*  <Select*/}
      {/*    multiple*/}
      {/*    label="Направление"*/}
      {/*    value={filter.directions}*/}
      {/*    onChange={(e) => setDirections(e.target.value as StockMovementDirection[])}*/}
      {/*    renderValue={(selected) =>*/}
      {/*      selected.length === 1*/}
      {/*        ? STOCK_MOVEMENT_DIRECTIONS.find((d) => d.value === selected[0])?.label*/}
      {/*        : `${selected.length} направления`*/}
      {/*    }*/}
      {/*  >*/}
      {/*    {STOCK_MOVEMENT_DIRECTIONS.map(({value, label}) => (*/}
      {/*      <MenuItem key={value} value={value}>*/}
      {/*        <Checkbox size="small" checked={filter.directions.includes(value)} />*/}
      {/*        <ListItemText primary={label} />*/}
      {/*      </MenuItem>*/}
      {/*    ))}*/}
      {/*  </Select>*/}
      {/*</FormControl>*/}

      <FormControl size="small" sx={{minWidth: 180}}>
        <InputLabel>Операция</InputLabel>
        <Select
          multiple
          label="Операция"
          value={filter.actions}
          onChange={(e) => setActions(e.target.value as string[])}
          renderValue={(selected) =>
            selected.length === 1
              ? STOCK_MOVEMENT_ACTIONS.find((a) => a.value === selected[0])?.label
              : `${selected.length} операции`
          }
        >
          {STOCK_MOVEMENT_ACTIONS.map(({value, label}) => (
            <MenuItem key={value} value={value}>
              <Checkbox size="small" checked={filter.actions.includes(value)} />
              <ListItemText primary={label} />
            </MenuItem>
          ))}
        </Select>
      </FormControl>

      <FormControlLabel
        control={
          <Checkbox
            size="small"
            checked={showTransfers}
            onChange={(e) => setShowTransfers(e.target.checked)}
          />
        }
        label="Показывать перемещения"
      />
    </FiltersBar>
  );
}

export default StockMovementsFilters;
