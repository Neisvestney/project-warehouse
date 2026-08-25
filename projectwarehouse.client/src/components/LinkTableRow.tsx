import {Children, cloneElement, isValidElement, type ReactElement} from "react";
import {Box, TableRow, type TableRowProps} from "@mui/material";
import {Link as RouterLink} from "react-router";

interface LinkTableRowProps extends TableRowProps {
  to: string;
  /** The overlay link has no visible text of its own, so every row needs one. */
  ariaLabel: string;
}

function LinkTableRow({to, ariaLabel, children, sx, ...props}: LinkTableRowProps) {
  const overlay = (
    <Box
      key="link-overlay"
      component={RouterLink}
      to={to}
      aria-label={ariaLabel}
      sx={{position: "absolute", inset: 0, zIndex: 0}}
    />
  );

  // the overlay lives inside the first cell: a bare <a> under <tr> gets wrapped in an anonymous cell
  const cells = Children.toArray(children);
  const first = cells[0];
  if (isValidElement<{children?: React.ReactNode}>(first)) {
    cells[0] = cloneElement(first as ReactElement<{children?: React.ReactNode}>, undefined, [
      overlay,
      first.props.children,
    ]);
  } else if (import.meta.env.DEV) {
    console.warn(
      `LinkTableRow (${to}): first child is not an element, the row will not be clickable`,
    );
  }

  return (
    <TableRow hover sx={[{position: "relative"}, ...(Array.isArray(sx) ? sx : [sx])]} {...props}>
      {cells}
    </TableRow>
  );
}

export default LinkTableRow;
