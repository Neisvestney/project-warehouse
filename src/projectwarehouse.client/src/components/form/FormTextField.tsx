import type {TextFieldProps} from "@mui/material";
import {TextField} from "@mui/material";
import type {Control, FieldValues, Path, RegisterOptions} from "react-hook-form";
import {Controller} from "react-hook-form";

interface FormTextFieldProps<T extends FieldValues> extends Omit<
  TextFieldProps,
  "name" | "error" | "helperText"
> {
  control: Control<T>;
  name: Path<T>;
  rules?: RegisterOptions<T, Path<T>>;
  helperText?: string;
}

export function FormTextField<T extends FieldValues>({
  control,
  name,
  rules,
  helperText,
  ...rest
}: FormTextFieldProps<T>) {
  return (
    <Controller
      control={control}
      name={name}
      rules={rules}
      render={({field, fieldState}) => (
        <TextField
          {...rest}
          {...field}
          error={!!fieldState.error}
          helperText={fieldState.error?.message ?? helperText}
        />
      )}
    />
  );
}
