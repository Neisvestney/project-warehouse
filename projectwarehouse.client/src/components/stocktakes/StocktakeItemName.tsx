import {Stack, Typography} from "@mui/material";
import {CatalogItemLink} from "@/components/catalog/CatalogItemLink";

interface StocktakeItemNameProps {
  catalogItemId: string;
  catalogItemName: string;
  inventoryNumber?: string | null;
  onOpen?: (id: string) => void;
}

function StocktakeItemName({
  catalogItemId,
  catalogItemName,
  inventoryNumber,
  onOpen,
}: StocktakeItemNameProps) {
  const content = (
    <Stack>
      <Typography variant="body2">{catalogItemName}</Typography>
      {inventoryNumber && (
        <Typography variant="caption" color="text.secondary" sx={{fontFamily: "monospace"}}>
          {inventoryNumber}
        </Typography>
      )}
    </Stack>
  );

  if (!onOpen) return content;
  return (
    <CatalogItemLink catalogItemId={catalogItemId} onOpen={onOpen}>
      {content}
    </CatalogItemLink>
  );
}

export default StocktakeItemName;
