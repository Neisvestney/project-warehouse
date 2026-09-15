import {Box, Checkbox, TableCell, type TableCellProps} from "@mui/material";

interface SelectionTableCellProps extends Omit<TableCellProps, "onClick" | "padding"> {
  checked: boolean;
  indeterminate?: boolean;
  /** `extendRange` is true for Shift+click. */
  onCheck: (extendRange: boolean) => void;
}

function SelectionTableCell({
  checked,
  indeterminate = false,
  onCheck,
  sx,
  children,
  ...props
}: SelectionTableCellProps) {
  return (
    // height: 1px lets the hit area's height: 100% resolve against the row height
    <TableCell
      padding="checkbox"
      sx={[{p: 0, height: "1px"}, ...(Array.isArray(sx) ? sx : [sx])]}
      {...props}
    >
      {/* LinkTableRow injects its link overlay here */}
      {children}
      {/* positioned above the LinkTableRow overlay; the cell itself must stay static */}
      <Box
        onClick={(e) => onCheck(e.shiftKey)}
        onMouseDown={(e) => {
          if (e.shiftKey) e.preventDefault();
        }}
        sx={{
          position: "relative",
          zIndex: 1,
          height: "100%",
          display: "flex",
          alignItems: "center",
          pl: 0.5,
          cursor: "pointer",
        }}
      >
        <Checkbox size="small" checked={checked} indeterminate={indeterminate} />
      </Box>
    </TableCell>
  );
}

export default SelectionTableCell;
