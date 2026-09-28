import {ToggleButton} from "@mui/material";
import {TOTAL_LABEL} from "./charts/chartSeries";

interface TotalToggleProps {
  value: boolean;
  onChange: (value: boolean) => void;
}

/** Shows or hides the line summing every channel. */
function TotalToggle({value, onChange}: TotalToggleProps) {
  return (
    <ToggleButton value="total" size="small" selected={value} onChange={() => onChange(!value)}>
      {TOTAL_LABEL}
    </ToggleButton>
  );
}

export default TotalToggle;
