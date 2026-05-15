import {InputAdornment, TextField, type TextFieldProps} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";

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
        },
      }}
    />
  );
}

export default SearchInput;
