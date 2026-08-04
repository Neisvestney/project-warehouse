import {
  Box,
  Divider,
  Drawer,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import {format} from "date-fns";
import {ru} from "date-fns/locale";
import type {AssemblyFulfillmentDto} from "@/api/types.gen";
import CatalogItemTypeChip from "@/components/catalog/CatalogItemTypeChip";
import {countFulfilledQty, getFulfillmentKind} from "@/components/orders/orderAssemblyUtils";
import {formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";
import {CatalogItemDrawer} from "@/components/catalog/CatalogItemDrawer";
import {CatalogItemLink} from "@/components/catalog/CatalogItemLink";
import {useDrawerSearchParamsState} from "@/hooks/useDrawerSearchParamsState";

function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return "—";
  try {
    return format(new Date(iso), "d MMM yyyy, HH:mm", {locale: ru});
  } catch {
    return iso;
  }
}

function formatNodePath(path: string[]): string {
  return path.length > 0 ? formatStoragePlaceNodeName(path) : "—";
}

interface FulfillmentCardProps {
  fulfillment: AssemblyFulfillmentDto;
  isVariation: boolean;
  catalogItemId?: string;
  openCatalogItemDrawer: (id: string) => void;
}

function FulfillmentCard({
  fulfillment,
  isVariation,
  catalogItemId,
  openCatalogItemDrawer,
}: FulfillmentCardProps) {
  const kind = getFulfillmentKind(fulfillment);
  const headline =
    kind === "unit"
      ? `Инв. № ${fulfillment.unitInventoryNumber ?? "—"}`
      : kind === "bundle"
        ? `Комплект (${fulfillment.bundleComponents.length} комп.)`
        : `× ${fulfillment.quantity}`;

  const headlineText = (
    <Typography variant="body2" sx={{fontWeight: 600}}>
      {headline}
    </Typography>
  );

  return (
    <Paper variant="outlined" sx={{p: 1.5}}>
      <Stack spacing={1}>
        {catalogItemId ? (
          <CatalogItemLink catalogItemId={catalogItemId} onOpen={openCatalogItemDrawer}>
            {headlineText}
          </CatalogItemLink>
        ) : (
          headlineText
        )}

        {isVariation &&
          (fulfillment.resolvedCatalogItemId ? (
            <CatalogItemLink
              catalogItemId={fulfillment.resolvedCatalogItemId}
              onOpen={openCatalogItemDrawer}
              sx={{flexWrap: "wrap"}}
            >
              <Typography variant="body2">
                Вариант: {fulfillment.resolvedCatalogItemName}
              </Typography>
              {fulfillment.resolvedCatalogItemType && (
                <CatalogItemTypeChip type={fulfillment.resolvedCatalogItemType} />
              )}
            </CatalogItemLink>
          ) : (
            <Typography variant="body2" color="text.disabled">
              Вариант не зафиксирован
            </Typography>
          ))}

        {kind === "bundle" ? (
          <Box sx={{overflowX: "auto"}}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Компонент</TableCell>
                  <TableCell>Ячейка</TableCell>
                  <TableCell sx={{width: 120}}>Кол-во</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {fulfillment.bundleComponents.map((c) => (
                  <TableRow key={c.id}>
                    <TableCell>
                      <CatalogItemLink
                        catalogItemId={c.catalogItemId}
                        onOpen={openCatalogItemDrawer}
                        spacing={0.5}
                      >
                        <Typography variant="body2">{c.catalogItemName}</Typography>
                        <CatalogItemTypeChip type={c.catalogItemType} />
                      </CatalogItemLink>
                    </TableCell>
                    <TableCell>
                      <Typography variant="caption">{formatNodePath(c.sourceNodePath)}</Typography>
                    </TableCell>
                    <TableCell>
                      <Typography variant="caption">
                        {c.unitInventoryItemId || c.unitInventoryNumber
                          ? `Инв. № ${c.unitInventoryNumber ?? "—"}`
                          : `× ${c.quantity}`}
                      </Typography>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Box>
        ) : (
          <Typography variant="body2" color="text.secondary">
            {formatNodePath(fulfillment.sourceNodePath)}
          </Typography>
        )}

        <Typography variant="caption" color="text.secondary">
          {fulfillment.createdByName ?? "—"} · {formatDateTime(fulfillment.createdAt)}
        </Typography>
      </Stack>
    </Paper>
  );
}

interface FulfillmentsDrawerProps {
  open: boolean;
  onClose: () => void;
  title: string;
  subtitle?: string;
  quantity: number;
  isVariation?: boolean;
  catalogItemId?: string;
  fulfillments: AssemblyFulfillmentDto[];
}

function FulfillmentsDrawer({
  open,
  onClose,
  title,
  subtitle,
  quantity,
  isVariation = false,
  catalogItemId,
  fulfillments,
}: FulfillmentsDrawerProps) {
  const [openedCatalogItemId, openCatalogDrawer, closeCatalogDrawer] =
    useDrawerSearchParamsState("fulfillmentCatalogItem");

  return (
    <Drawer
      anchor="right"
      open={open}
      onClose={onClose}
      slotProps={{paper: {sx: {width: {xs: "100%", sm: 480}}}}}
    >
      <Stack spacing={1} sx={{p: 2}}>
        <Typography variant="h6">{title}</Typography>
        {subtitle && (
          <Typography variant="caption" color="text.secondary">
            {subtitle}
          </Typography>
        )}
        <Typography variant="body2" color="text.secondary">
          Собрано {countFulfilledQty(fulfillments)} из {quantity}
        </Typography>
      </Stack>
      <Divider />
      <Stack spacing={1.5} sx={{p: 2}}>
        {fulfillments.length === 0 ? (
          <Typography variant="body2" color="text.secondary">
            Позиция ещё не собрана
          </Typography>
        ) : (
          fulfillments.map((f) => (
            <FulfillmentCard
              key={f.id}
              fulfillment={f}
              isVariation={isVariation}
              catalogItemId={catalogItemId}
              openCatalogItemDrawer={openCatalogDrawer}
            />
          ))
        )}
      </Stack>
      <CatalogItemDrawer
        itemId={openedCatalogItemId}
        onClose={closeCatalogDrawer}
        onOpenItem={openCatalogDrawer}
      />
    </Drawer>
  );
}

export default FulfillmentsDrawer;
