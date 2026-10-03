import type {ReactNode} from "react";
import {Button, Checkbox, FormControlLabel, Stack} from "@mui/material";
import type {MarketplaceType} from "@/api/types.gen";
import {MARKETPLACE_TYPE_LABELS} from "@/components/marketplace/marketplaceUtils.ts";

const SHORTCUT_TYPES: MarketplaceType[] = ["ozon", "wildberries"];

export interface PickableMarketplaceAccount {
  id: string;
  name: string;
  type: MarketplaceType;
}

interface MarketplaceAccountPickerProps<T extends PickableMarketplaceAccount> {
  accounts: T[];
  selected: Set<string>;
  onChange: (selected: Set<string>) => void;
  /** Rendered at the end of the row, e.g. a warning icon explaining what the run will skip. */
  renderExtra?: (account: T) => ReactNode;
}

/** Checkbox list of marketplace accounts with "all of a marketplace" shortcuts. */
function MarketplaceAccountPicker<T extends PickableMarketplaceAccount>({
  accounts,
  selected,
  onChange,
  renderExtra,
}: MarketplaceAccountPickerProps<T>) {
  function toggle(id: string) {
    const next = new Set(selected);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    onChange(next);
  }

  function selectAllOfType(type: MarketplaceType) {
    const ids = accounts.filter((a) => a.type === type).map((a) => a.id);
    onChange(new Set([...selected, ...ids]));
  }

  return (
    <Stack spacing={1}>
      <Stack direction="row" spacing={1}>
        {SHORTCUT_TYPES.filter((type) => accounts.some((a) => a.type === type)).map((type) => (
          <Button key={type} size="small" onClick={() => selectAllOfType(type)}>
            Все {MARKETPLACE_TYPE_LABELS[type]}
          </Button>
        ))}
      </Stack>
      {accounts.map((account) => (
        <Stack key={account.id} direction="row" spacing={1} sx={{alignItems: "center"}}>
          <FormControlLabel
            control={
              <Checkbox
                size="small"
                checked={selected.has(account.id)}
                onChange={() => toggle(account.id)}
              />
            }
            label={`${account.name} · ${MARKETPLACE_TYPE_LABELS[account.type]}`}
            sx={{flexGrow: 1}}
          />
          {renderExtra?.(account)}
        </Stack>
      ))}
    </Stack>
  );
}

export default MarketplaceAccountPicker;
