import {ToggleButton, ToggleButtonGroup} from "@mui/material";

interface ShareToggleProps {
  share: boolean;
  onChange: (share: boolean) => void;
  /** What the count is of, as the first button reads. */
  countLabel: string;
}

/** Switches a chart between a count and its share of the base. */
function ShareToggle({share, onChange, countLabel}: ShareToggleProps) {
  return (
    <ToggleButtonGroup
      exclusive
      size="small"
      value={share ? "share" : "count"}
      onChange={(_, value: "share" | "count" | null) => value && onChange(value === "share")}
    >
      <ToggleButton value="count">{countLabel}</ToggleButton>
      <ToggleButton value="share">%</ToggleButton>
    </ToggleButtonGroup>
  );
}

export default ShareToggle;
