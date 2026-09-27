import {MenuItem, TextField} from "@mui/material";
import type {CardChannelOption} from "./useCardChannel";

interface CardChannelSelectProps {
  options: CardChannelOption[];
  value: string | null;
  onChange: (value: string | null) => void;
}

const ALL = "";

/** «Every channel» or one of the page's, for a card that shows one line of them. */
function CardChannelSelect({options, value, onChange}: CardChannelSelectProps) {
  return (
    <TextField
      select
      size="small"
      label="Канал"
      value={value ?? ALL}
      onChange={(e) => onChange(e.target.value || null)}
      slotProps={{select: {displayEmpty: true}, inputLabel: {shrink: true}}}
      sx={{width: 180}}
    >
      {options.map((o) => (
        <MenuItem key={o.value ?? ALL} value={o.value ?? ALL}>
          {o.label}
        </MenuItem>
      ))}
    </TextField>
  );
}

export default CardChannelSelect;
