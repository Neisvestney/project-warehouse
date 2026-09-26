import {IconButton, InputAdornment, TextField, type TextFieldProps} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import ClearIcon from "@mui/icons-material/Clear";

type SearchInputProps = Omit<TextFieldProps, "onChange" | "value"> & {
  value: string;
  onChange: (value: string) => void;
};

function SearchInput({value, onChange, label = "Поиск", ...rest}: SearchInputProps) {
  return (
    <TextField
      size="small"
      {...rest}
      label={label}
      value={value}
      onChange={(e) => onChange(e.target.value)}
      slotProps={{
        input: {
          startAdornment: (
            <InputAdornment position="start">
              <SearchIcon />
            </InputAdornment>
          ),
          endAdornment: value ? (
            <InputAdornment position="end">
              <IconButton
                size="small"
                edge="end"
                aria-label="Очистить"
                onClick={() => onChange("")}
              >
                <ClearIcon fontSize="small" />
              </IconButton>
            </InputAdornment>
          ) : undefined,
        },
      }}
    />
  );
}

export default SearchInput;
