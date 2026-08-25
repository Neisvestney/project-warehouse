import {Autocomplete, TextField} from "@mui/material";
import type {Control, FieldValues, Path} from "react-hook-form";
import {Controller} from "react-hook-form";

// Chrome only learned supportedValuesOf in 99; the field stays usable as free text without it.
const TIME_ZONE_OPTIONS: string[] =
  typeof Intl.supportedValuesOf === "function" ? Intl.supportedValuesOf("timeZone") : [];

interface FormTimeZoneFieldProps<T extends FieldValues> {
  control: Control<T>;
  name: Path<T>;
  label?: string;
  placeholder?: string;
  helperText?: string;
  disabled?: boolean;
  size?: "small" | "medium";
  fullWidth?: boolean;
  sx?: React.ComponentProps<typeof Autocomplete>["sx"];
}

export function FormTimeZoneField<T extends FieldValues>({
  control,
  name,
  label = "Часовой пояс",
  placeholder,
  helperText,
  disabled,
  size,
  fullWidth,
  sx,
}: FormTimeZoneFieldProps<T>) {
  return (
    <Controller
      control={control}
      name={name}
      render={({field, fieldState}) => (
        <Autocomplete
          freeSolo
          options={TIME_ZONE_OPTIONS}
          value={(field.value as string | null) ?? null}
          onChange={(_, v) => field.onChange(v ?? null)}
          onInputChange={(_, v, reason) => {
            if (reason === "input") field.onChange(v || null);
          }}
          disabled={disabled}
          size={size}
          fullWidth={fullWidth}
          sx={sx}
          renderInput={(params) => (
            <TextField
              {...params}
              label={label}
              placeholder={placeholder}
              error={!!fieldState.error}
              helperText={fieldState.error?.message ?? helperText}
            />
          )}
        />
      )}
    />
  );
}
