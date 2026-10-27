import {Box, Tooltip, Typography} from "@mui/material";
import FilterAltIcon from "@mui/icons-material/FilterAlt";
import HoverActionLink from "@/components/HoverActionLink";
import type {MarketplaceSupplyDto} from "@/api/types.gen";
import {formatSupplyDate, supplyName} from "./marketplaceSupplyUtils";

function SupplyLines({supply}: {supply: MarketplaceSupplyDto}) {
  return (
    <Box sx={{minWidth: 0}}>
      <Typography variant="body2" sx={{lineHeight: 1.25, whiteSpace: "nowrap"}}>
        {formatSupplyDate(supply)}
      </Typography>
      <Typography
        variant="caption"
        color="text.secondary"
        sx={{display: "block", lineHeight: 1.15, overflowWrap: "anywhere"}}
      >
        {supplyName(supply)}
      </Typography>
    </Box>
  );
}

interface MarketplaceSupplyLabelProps {
  supply: MarketplaceSupplyDto;
  /** Turns the label into a button that filters the list by this supply. */
  onFilter?: (supplyId: string) => void;
}

/** Creation date over the name, the supply id in a tooltip. */
function MarketplaceSupplyLabel({supply, onFilter}: MarketplaceSupplyLabelProps) {
  return (
    <Tooltip title={supply.externalId}>
      {onFilter ? (
        <HoverActionLink
          icon={FilterAltIcon}
          onClick={() => onFilter(supply.id)}
          ariaLabel={`Показать заказы поставки ${supplyName(supply)}`}
          spacing={0.5}
          sx={{position: "relative", zIndex: 1}}
        >
          <SupplyLines supply={supply} />
        </HoverActionLink>
      ) : (
        <Box sx={{width: "fit-content"}}>
          <SupplyLines supply={supply} />
        </Box>
      )}
    </Tooltip>
  );
}

export default MarketplaceSupplyLabel;
