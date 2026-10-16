import {MenuItem, TextField, ToggleButton, ToggleButtonGroup} from "@mui/material";
import type {AnalyticsTopItemsBy} from "@/api/types.gen";
import CardChannelSelect from "./CardChannelSelect";
import type {useTopItemsState} from "./useTopItems";

interface TopItemsControlsProps {
  state: ReturnType<typeof useTopItemsState>;
  /** Currencies the last response met; the switch shows only when there is a choice. */
  currencies: string[];
  currencyCode: string | null | undefined;
}

/** Channel, ranking and currency of the top. */
function TopItemsControls({state, currencies, currencyCode}: TopItemsControlsProps) {
  return (
    <>
      <CardChannelSelect
        options={state.options}
        value={state.channel}
        onChange={state.setChannel}
      />
      <ToggleButtonGroup
        exclusive
        size="small"
        value={state.by}
        onChange={(_, value: AnalyticsTopItemsBy | null) => value && state.setBy(value)}
      >
        <ToggleButton value="units">Штуки</ToggleButton>
        <ToggleButton value="money" disabled={!state.canRankByMoney}>
          Деньги
        </ToggleButton>
      </ToggleButtonGroup>
      {currencies.length > 1 && currencyCode && (
        <TextField
          select
          size="small"
          label="Валюта"
          value={currencyCode}
          onChange={(e) => state.setCurrency(e.target.value)}
          sx={{width: 100}}
        >
          {currencies.map((c) => (
            <MenuItem key={c} value={c}>
              {c}
            </MenuItem>
          ))}
        </TextField>
      )}
    </>
  );
}

export default TopItemsControls;
