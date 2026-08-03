import {useEffect, useRef, useState} from "react";
import type {TextFieldProps} from "@mui/material";
import {TextField} from "@mui/material";

interface ClampedIntegerFieldProps extends Omit<
  TextFieldProps,
  "value" | "onChange" | "onBlur" | "onFocus" | "type"
> {
  value: number;
  min?: number;
  max?: number;
  onCommit: (value: number) => void;
}

/**
 * Number field that keeps raw keystrokes (including an empty field) uncommitted
 * until blur, so clamping doesn't fight the user while they're typing/clearing it.
 */
export function ClampedIntegerField({
  value,
  min = 1,
  max,
  onCommit,
  slotProps,
  ...rest
}: ClampedIntegerFieldProps) {
  const [raw, setRaw] = useState(String(value));
  const focusedRef = useRef(false);

  useEffect(() => {
    if (!focusedRef.current) setRaw(String(value));
  }, [value]);

  return (
    <TextField
      type="number"
      value={raw}
      onFocus={() => {
        focusedRef.current = true;
      }}
      onChange={(e) => {
        const next = e.target.value;
        if (next !== "" && !/^\d+$/.test(next)) return;
        setRaw(next);
      }}
      onBlur={() => {
        focusedRef.current = false;
        let n = Math.max(min, Number(raw) || min);
        if (max !== undefined) n = Math.min(max, n);
        setRaw(String(n));
        if (n !== value) onCommit(n);
      }}
      slotProps={{
        ...slotProps,
        htmlInput: {min, ...(max !== undefined ? {max} : {}), ...slotProps?.htmlInput},
      }}
      {...rest}
    />
  );
}
