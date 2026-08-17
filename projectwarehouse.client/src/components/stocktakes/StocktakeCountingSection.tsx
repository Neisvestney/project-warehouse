import {Alert, Button, Chip, Paper, Stack, Typography} from "@mui/material";
import DifferenceIcon from "@mui/icons-material/Difference";
import {useHasPermission} from "@/hooks/usePermission";
import {useDrawerSearchParamsState} from "@/hooks/useDrawerSearchParamsState";
import {CatalogItemDrawer} from "@/components/catalog/CatalogItemDrawer";
import StocktakeNodeAccordion from "@/components/stocktakes/StocktakeNodeAccordion";
import type {StocktakeDto} from "@/api/types.gen";

interface StocktakeCountingSectionProps {
  stocktake: StocktakeDto;
  onUpdated: (updated: StocktakeDto) => void;
  onShowDifferences: () => void;
}

function StocktakeCountingSection({
  stocktake,
  onUpdated,
  onShowDifferences,
}: StocktakeCountingSectionProps) {
  const canEdit = useHasPermission(["stocktakes.edit", "stocktakes.edit_assigned"]);
  const [openedCatalogItemId, openCatalogDrawer, closeCatalogDrawer] =
    useDrawerSearchParamsState("catalogItem");

  return (
    <Paper>
      <Stack spacing={2} sx={{p: 3}}>
        <Stack direction="row" spacing={1.5} sx={{alignItems: "center"}}>
          <Typography variant="h6">Пересчёт</Typography>
          <Chip label={`${stocktake.nodes.length} яч.`} size="small" />
          <div style={{flexGrow: 1}} />
          <Button size="small" startIcon={<DifferenceIcon />} onClick={onShowDifferences}>
            Показать расхождения
          </Button>
        </Stack>

        <Alert severity="info">
          Раскройте ячейку, чтобы подставить её текущий остаток. По умолчанию факт равен учёту —
          меняйте только расхождения. Каждая ячейка сохраняется отдельно.
        </Alert>

        <Stack>
          {stocktake.nodes.map((node) => (
            <StocktakeNodeAccordion
              key={node.id}
              stocktake={stocktake}
              node={node}
              canEdit={canEdit}
              onUpdated={onUpdated}
              onOpenCatalogItem={openCatalogDrawer}
            />
          ))}
        </Stack>
      </Stack>

      <CatalogItemDrawer
        itemId={openedCatalogItemId}
        onClose={closeCatalogDrawer}
        onOpenItem={openCatalogDrawer}
      />
    </Paper>
  );
}

export default StocktakeCountingSection;
