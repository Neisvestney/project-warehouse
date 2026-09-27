import {Checkbox, FormControl, InputLabel, ListItemText, MenuItem, Select} from "@mui/material";
import {useQuery} from "@tanstack/react-query";
import {marketplacesGetAccountsShortOptions} from "@/api/@tanstack/react-query.gen";
import type {MarketplaceType} from "@/api/types.gen";
import {
  ALL_MARKETPLACE_TYPES,
  MARKETPLACE_LABELS,
} from "@/components/orders/marketplace/marketplaceOrderUtils";
import {DIRECT_CHANNEL} from "./channelsQuery";

const TYPE_PREFIX = "type:";
const ALL_CHANNELS = "all";

interface ChannelsSelectProps {
  /** Account ids and {@link DIRECT_CHANNEL}; empty means every channel. */
  value: string[];
  onChange: (value: string[]) => void;
  /** Off for a chart that only shops have data for. */
  withDirect?: boolean;
}

function ChannelsSelect({value, onChange, withDirect = true}: ChannelsSelectProps) {
  const {data: accounts, isPending} = useQuery(marketplacesGetAccountsShortOptions());

  const allIds = [...(accounts ?? []).map((a) => a.id), ...(withDirect ? [DIRECT_CHANNEL] : [])];
  const selected = value.length === 0 ? allIds : value;
  const accountIdsOf = (type: MarketplaceType) =>
    (accounts ?? []).filter((a) => a.type === type).map((a) => a.id);

  function commit(next: string[]) {
    // Everything or nothing both read as "all channels", which the URL keeps as no param at all
    const ordered = allIds.filter((id) => next.includes(id));
    onChange(ordered.length === 0 || ordered.length === allIds.length ? [] : ordered);
  }

  function handleChange(next: string[]) {
    const toggled =
      next.find((v) => !selected.includes(v)) ?? selected.find((v) => !next.includes(v));
    if (!toggled) return;

    if (toggled === ALL_CHANNELS) return onChange([]);

    if (toggled.startsWith(TYPE_PREFIX)) {
      const ids = accountIdsOf(toggled.slice(TYPE_PREFIX.length) as MarketplaceType);
      const allOn = ids.every((id) => selected.includes(id));
      return commit(
        allOn ? selected.filter((id) => !ids.includes(id)) : [...new Set([...selected, ...ids])],
      );
    }

    commit(next.filter((v) => !v.startsWith(TYPE_PREFIX)));
  }

  function renderValue() {
    if (value.length === 0) return withDirect ? "Все каналы" : "Все магазины";
    if (!accounts && isPending) return "Загрузка…";

    const parts: string[] = [];
    for (const type of ALL_MARKETPLACE_TYPES) {
      const ids = accountIdsOf(type);
      if (ids.length > 0 && ids.every((id) => value.includes(id)))
        parts.push(MARKETPLACE_LABELS[type]);
      else
        parts.push(
          ...(accounts ?? [])
            .filter((a) => a.type === type && value.includes(a.id))
            .map((a) => a.name),
        );
    }
    if (value.includes(DIRECT_CHANNEL)) parts.push("Прямые");
    return parts.join(", ");
  }

  return (
    <FormControl size="small" sx={{width: 240}}>
      <InputLabel shrink>Каналы</InputLabel>
      <Select
        multiple
        displayEmpty
        notched
        label="Каналы"
        value={selected}
        onChange={(e) => {
          const next = e.target.value;
          handleChange(typeof next === "string" ? next.split(",") : next);
        }}
        renderValue={renderValue}
      >
        <MenuItem value={ALL_CHANNELS}>
          <Checkbox size="small" checked={value.length === 0} />
          <ListItemText primary={withDirect ? "Все каналы" : "Все магазины"} />
        </MenuItem>
        {ALL_MARKETPLACE_TYPES.flatMap((type) => {
          const ids = accountIdsOf(type);
          const count = ids.filter((id) => selected.includes(id)).length;
          return [
            <MenuItem key={type} value={`${TYPE_PREFIX}${type}`} disabled={ids.length === 0}>
              <Checkbox
                size="small"
                checked={ids.length > 0 && count === ids.length}
                indeterminate={count > 0 && count < ids.length}
              />
              <ListItemText
                primary={MARKETPLACE_LABELS[type]}
                slotProps={{primary: {sx: {fontWeight: 600}}}}
              />
            </MenuItem>,
            ...(accounts ?? [])
              .filter((a) => a.type === type)
              .map((a) => (
                <MenuItem key={a.id} value={a.id} sx={{pl: 4}}>
                  <Checkbox size="small" checked={selected.includes(a.id)} />
                  <ListItemText primary={a.name} />
                </MenuItem>
              )),
          ];
        })}
        {withDirect && (
          <MenuItem value={DIRECT_CHANNEL}>
            <Checkbox size="small" checked={selected.includes(DIRECT_CHANNEL)} />
            <ListItemText primary="Прямые" slotProps={{primary: {sx: {fontWeight: 600}}}} />
          </MenuItem>
        )}
      </Select>
    </FormControl>
  );
}

export default ChannelsSelect;
