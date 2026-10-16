import {useMemo, useState} from "react";
import {
  Autocomplete,
  Button,
  Chip,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import LocationOnIcon from "@mui/icons-material/LocationOn";
import {useQuery} from "@tanstack/react-query";
import {inventoryItemsGetAllUnitsOptions} from "@/api/@tanstack/react-query.gen";
import type {UnitInventoryItemDto} from "@/api/types.gen";
import SelectNodeModal, {type SelectedNode} from "@/components/receipts/SelectNodeModal";
import {formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";
import {useDebounce} from "@/hooks/useDebounce";
import {NeedHint} from "./ComponentRow";
import {isShort} from "./fulfillmentStatus";

interface NodePick {
  nodeId: string;
  nodePath: string;
}

function toNodePick(node: SelectedNode): NodePick {
  return {nodeId: node.nodeId, nodePath: formatStoragePlaceNodeName(node.nodePath)};
}

interface NodeControlProps {
  warehouseId: string;
  catalogItemId: string;
  node: NodePick | null;
  /** The cell came from the warehouse default, not from a pick. */
  isDefault?: boolean;
  /** Pieces in the cell, `null` while unknown. */
  available: number | null;
  /** How many pieces this row will take out of the cell. */
  needQty?: number;
  /** Multiplied need — batch assembly copies one composition across several tasks. */
  needTimes?: number;
  onSelect: (node: NodePick) => void;
}

/** Cell of a standard row: path, what lies there, and the picker. */
function NodeControl({
  warehouseId,
  catalogItemId,
  node,
  isDefault,
  available,
  needQty,
  needTimes = 1,
  onSelect,
}: NodeControlProps) {
  const [open, setOpen] = useState(false);
  const short = isShort(available, needQty, needTimes);

  return (
    <Stack direction="row" spacing={1} sx={{alignItems: "center", flexGrow: 1, minWidth: 0}}>
      {/* The cell and its chips take the free space, which parks the button on the right edge —
          an `ml: auto` on the button would lose to Stack's own spacing margin. */}
      <Stack
        direction="row"
        spacing={1}
        sx={{alignItems: "center", flexWrap: "wrap", rowGap: 0.5, flexGrow: 1, minWidth: 0}}
      >
        {node ? (
          <Typography variant="caption" sx={{fontFamily: "monospace"}}>
            {node.nodePath}
          </Typography>
        ) : (
          <NeedHint>Ячейка не выбрана</NeedHint>
        )}
        {node && available !== null && (
          <Chip
            size="small"
            color={short ? "error" : "success"}
            label={
              available === 0
                ? "в ячейке пусто"
                : short && needTimes > 1
                  ? `остаток ${available} — на ${needTimes} не хватит`
                  : short
                    ? `остаток ${available} — меньше нужного`
                    : needTimes > 1
                      ? `хватит на ${needTimes}`
                      : `остаток ${available}`
            }
          />
        )}
        {node && isDefault && <Chip size="small" variant="outlined" label="по умолчанию" />}
      </Stack>
      <Button
        size="small"
        variant="outlined"
        startIcon={<LocationOnIcon />}
        onClick={() => setOpen(true)}
        sx={{flexShrink: 0}}
      >
        {node ? "Изменить" : "Выбрать"}
      </Button>
      <SelectNodeModal
        open={open}
        onClose={() => setOpen(false)}
        warehouseId={warehouseId}
        catalogItemId={catalogItemId}
        onSelect={(picked) => {
          onSelect(toNodePick(picked));
          setOpen(false);
        }}
      />
    </Stack>
  );
}

interface VariantPickerProps {
  label: string;
  options: {id: string; fullName: string}[];
  value: string | null;
  onChange: (id: string) => void;
}

const CHIP_PICKER_LIMIT = 5;

/** Few variants read better as a row of chips — one tap instead of open-scroll-pick. */
function VariantPicker({label, options, value, onChange}: VariantPickerProps) {
  if (options.length > CHIP_PICKER_LIMIT) {
    return (
      <FormControl size="small" fullWidth error={!value}>
        <InputLabel>{label}</InputLabel>
        <Select value={value ?? ""} label={label} onChange={(e) => onChange(e.target.value)}>
          {options.map((o) => (
            <MenuItem key={o.id} value={o.id}>
              {o.fullName}
            </MenuItem>
          ))}
        </Select>
      </FormControl>
    );
  }

  return (
    <Stack direction="row" spacing={0.75} sx={{flexWrap: "wrap", rowGap: 0.75}}>
      {options.map((o) => (
        <Chip
          key={o.id}
          size="small"
          label={o.fullName}
          color={value === o.id ? "primary" : "default"}
          variant={value === o.id ? "filled" : "outlined"}
          onClick={() => onChange(o.id)}
        />
      ))}
    </Stack>
  );
}

interface UnitPickerProps {
  catalogItemId: string;
  warehouseId: string;
  value: UnitInventoryItemDto | null;
  onChange: (item: UnitInventoryItemDto | null) => void;
}

/** Instance of a unit item: scan or search by inventory number, optionally narrowed to a cell. */
function UnitPicker({catalogItemId, warehouseId, value, onChange}: UnitPickerProps) {
  const [inputValue, setInputValue] = useState("");
  const debouncedInput = useDebounce(inputValue, 300);
  const [nodeFilter, setNodeFilter] = useState<NodePick | null>(null);
  const [nodeModalOpen, setNodeModalOpen] = useState(false);

  const query = useQuery({
    ...inventoryItemsGetAllUnitsOptions({
      query: {
        catalogItemId,
        warehouseId,
        pageSize: 50,
        searchString: debouncedInput || undefined,
        nodeId: nodeFilter?.nodeId,
      },
    }),
  });

  const options = useMemo(() => {
    const results = query.data?.items ?? [];
    if (value && !results.some((i) => i.id === value.id)) return [value, ...results];
    return results;
  }, [query.data, value]);

  return (
    <Stack spacing={1}>
      <Autocomplete
        size="small"
        options={options}
        value={value}
        onChange={(_, item) => onChange(item)}
        inputValue={inputValue}
        onInputChange={(_, v) => setInputValue(v)}
        getOptionLabel={(item) =>
          `#${item.inventoryNumber} — ${item.storagePlaceName} / ${item.nodeName}`
        }
        isOptionEqualToValue={(o, v) => o.id === v.id}
        filterOptions={(x) => x}
        loading={query.isLoading}
        noOptionsText="Нет доступных экземпляров"
        renderInput={(params) => (
          <TextField
            {...params}
            label="Экземпляр — отсканируйте или введите номер"
            error={!value}
          />
        )}
      />
      <Stack direction="row" spacing={1} sx={{alignItems: "center", flexWrap: "wrap"}}>
        <Typography variant="caption" color="text.secondary">
          {nodeFilter ? `фильтр по ячейке: ${nodeFilter.nodePath}` : "фильтр по ячейке: любая"}
        </Typography>
        <Button size="small" startIcon={<LocationOnIcon />} onClick={() => setNodeModalOpen(true)}>
          Выбрать
        </Button>
        {nodeFilter && (
          <Button size="small" color="inherit" onClick={() => setNodeFilter(null)}>
            Сбросить
          </Button>
        )}
      </Stack>
      <SelectNodeModal
        open={nodeModalOpen}
        onClose={() => setNodeModalOpen(false)}
        warehouseId={warehouseId}
        catalogItemId={catalogItemId}
        onSelect={(picked) => {
          setNodeFilter(toNodePick(picked));
          setNodeModalOpen(false);
        }}
      />
    </Stack>
  );
}

export type {NodePick};
export {NodeControl, UnitPicker, VariantPicker};
