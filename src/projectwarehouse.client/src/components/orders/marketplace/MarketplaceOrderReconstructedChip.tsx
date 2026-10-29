import {Chip, Tooltip} from "@mui/material";

interface MarketplaceOrderReconstructedChipProps {
  value: boolean;
}

function MarketplaceOrderReconstructedChip({value}: MarketplaceOrderReconstructedChipProps) {
  if (!value) return null;
  return (
    <Tooltip title="Площадка уже не хранит этот заказ — он восстановлен из отчёта о реализации. Статус выведен из начислений, цена — из отчёта">
      <Chip size="small" variant="outlined" label="Из отчёта" />
    </Tooltip>
  );
}

export default MarketplaceOrderReconstructedChip;
