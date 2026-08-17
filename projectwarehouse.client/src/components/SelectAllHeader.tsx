import {Button, Stack} from "@mui/material";

type SelectAllHeaderProps = {
  onSelectAll: () => void;
  onClear: () => void;
  selectAllDisabled?: boolean;
  clearDisabled?: boolean;
  selectAllLabel?: string;
  clearLabel?: string;
};

/** Pinned "select all / clear" row for multiselect dropdowns (Select menu children, Autocomplete `paper` slot). */
function SelectAllHeader({
  onSelectAll,
  onClear,
  selectAllDisabled,
  clearDisabled,
  selectAllLabel = "Выбрать все",
  clearLabel = "Снять выбор",
}: SelectAllHeaderProps) {
  return (
    <Stack
      direction="row"
      spacing={1}
      sx={{p: 1, borderBottom: 1, borderColor: "divider"}}
      onMouseDown={(e) => e.preventDefault()}
      // Keep the dropdown's own navigation from hijacking the buttons, but let it still close
      onKeyDown={(e) => {
        if (e.key !== "Escape" && e.key !== "Tab") e.stopPropagation();
      }}
    >
      <Button size="small" disabled={selectAllDisabled} onClick={onSelectAll}>
        {selectAllLabel}
      </Button>
      <Button size="small" color="inherit" disabled={clearDisabled} onClick={onClear}>
        {clearLabel}
      </Button>
    </Stack>
  );
}

export default SelectAllHeader;
