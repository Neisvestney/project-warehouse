import {Checkbox, FormControl, InputLabel, ListItemText, MenuItem, Select} from "@mui/material";
import type {SxProps, Theme} from "@mui/material";
import type {CatalogItemType} from "@/api/types.gen";
import SelectAllHeader from "@/components/SelectAllHeader";
import {CATALOG_ITEM_TYPE_CONFIG, CATALOG_ITEM_TYPES} from "@/features/catalog";
import {NOUNS, pluralCount} from "@/utils/pluralUtils";

type CatalogTypesFilterProps = {
  value: CatalogItemType[];
  onChange: (value: CatalogItemType[]) => void;
  /** Types offered by this filter; the full set renders as "Все". */
  options?: CatalogItemType[];
  label?: string;
  sx?: SxProps<Theme>;
};

function CatalogTypesFilter({
  value,
  onChange,
  options = CATALOG_ITEM_TYPES,
  label = "Тип",
  sx,
}: CatalogTypesFilterProps) {
  return (
    <FormControl size="small" sx={[{minWidth: 150}, ...(Array.isArray(sx) ? sx : [sx])]}>
      <InputLabel>{label}</InputLabel>
      <Select
        multiple
        label={label}
        value={value}
        onChange={(e) => onChange(e.target.value as CatalogItemType[])}
        renderValue={(selected) => {
          if (selected.length === options.length) return "Все";
          if (selected.length === 0) return "Нет";
          if (selected.length === 1) return CATALOG_ITEM_TYPE_CONFIG[selected[0]].label;
          return pluralCount(selected.length, NOUNS.itemType);
        }}
      >
        <SelectAllHeader
          selectAllDisabled={value.length === options.length}
          clearDisabled={value.length === 0}
          onSelectAll={() => onChange([...options])}
          onClear={() => onChange([])}
        />
        {options.map((type) => (
          <MenuItem key={type} value={type}>
            <Checkbox checked={value.includes(type)} size="small" />
            <ListItemText primary={CATALOG_ITEM_TYPE_CONFIG[type].label} />
          </MenuItem>
        ))}
      </Select>
    </FormControl>
  );
}

export default CatalogTypesFilter;
