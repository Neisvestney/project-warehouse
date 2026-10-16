import {useState} from "react";
import {TextField, type TextFieldProps} from "@mui/material";

type DateFieldProps = Omit<TextFieldProps, "type" | "value" | "onChange"> & {
  /** `yyyy-MM-dd`, or null for an empty field. */
  value: string | null;
  onChange: (value: string | null) => void;
};

/**
 * Native date input whose value may arrive late, as a URL param does. While focused it shows its own draft:
 * re-rendering with the stale value would reset the segment being typed, so "15" would end up as "05".
 */
function DateField({value, onChange, onFocus, onBlur, slotProps, ...props}: DateFieldProps) {
  const [draft, setDraft] = useState<string | null>(null);

  return (
    <TextField
      {...props}
      type="date"
      value={draft ?? value ?? ""}
      onFocus={(e) => {
        setDraft(value ?? "");
        onFocus?.(e);
      }}
      onBlur={(e) => {
        setDraft(null);
        onBlur?.(e);
      }}
      onChange={(e) => {
        setDraft(e.target.value);
        onChange(e.target.value || null);
      }}
      slotProps={{inputLabel: {shrink: true}, ...slotProps}}
    />
  );
}

export default DateField;
