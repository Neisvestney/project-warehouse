import {Chip} from "@mui/material";
import type {MarketplaceCardDto} from "@/api/types.gen";
import {MAPPING_SOURCE_LABELS} from "../marketplaceUtils";

interface CardMappingChipProps {
  card: MarketplaceCardDto;
}

function CardMappingChip({card}: CardMappingChipProps) {
  // Устаревшая привязка важнее источника — её и показываем
  if (card.isMappedToArchivedItem) {
    return <Chip label="Привязана к архивному товару" color="warning" size="small" />;
  }
  if (!card.mappingSource) return null;
  return <Chip label={MAPPING_SOURCE_LABELS[card.mappingSource]} size="small" variant="outlined" />;
}

export default CardMappingChip;
