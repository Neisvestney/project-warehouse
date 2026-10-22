import {Menu, MenuItem} from "@mui/material";

interface PrintQuantityMenuProps {
  anchorEl: HTMLElement | null;
  onClose: () => void;
  onPrint: (withQuantity: boolean) => void;
  /** Column name of the system quantity, e.g. «План». */
  quantityLabel: string;
}

/** Asks whether a paper sheet prints its system quantity column or leaves the count blind. */
function PrintQuantityMenu({anchorEl, onClose, onPrint, quantityLabel}: PrintQuantityMenuProps) {
  const select = (withQuantity: boolean) => {
    onClose();
    onPrint(withQuantity);
  };

  return (
    <Menu
      anchorEl={anchorEl}
      open={anchorEl !== null}
      onClose={onClose}
      // Keeps clicks inside the menu from reaching an ancestor such as an accordion summary
      onClick={(e) => e.stopPropagation()}
    >
      <MenuItem onClick={() => select(true)}>С колонкой «{quantityLabel}»</MenuItem>
      <MenuItem onClick={() => select(false)}>Без колонки «{quantityLabel}»</MenuItem>
    </Menu>
  );
}

export default PrintQuantityMenu;
