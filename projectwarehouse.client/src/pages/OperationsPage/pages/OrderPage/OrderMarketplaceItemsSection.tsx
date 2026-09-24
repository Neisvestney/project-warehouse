import type {OrderDetailsDto, OrderMarketplaceItemDto} from "@/api";
import {
  Chip,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  Stack,
  Typography,
  Box,
  Paper,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import CardImage from "@/pages/SettingsPage/pages/MarketplacesSettingsPage/components/CardImage.tsx";
import CopyableText from "@/components/CopyableText.tsx";
import {useOpenCatalogItem} from "@/components/catalog/CatalogItemDrawerContext.ts";
import CatalogItemLink from "@/components/catalog/CatalogItemLink.tsx";
import InfoRow from "@/components/InfoRow.tsx";

function ReturnedChip({item}: {item: OrderMarketplaceItemDto}) {
  if (item.returnedQuantity <= 0) return null;
  const isFull = item.returnedQuantity >= item.quantity;
  return (
    <Chip
      size="small"
      color={isFull ? "error" : "warning"}
      label={isFull ? "Возвращено" : `Возвращено ${item.returnedQuantity} из ${item.quantity}`}
    />
  );
}

interface OrderBoxesSectionProps {
  order: OrderDetailsDto;
}

function OrderMarketplaceItemsSection({order}: OrderBoxesSectionProps) {
  const openCatalogItem = useOpenCatalogItem();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("md"));

  if (isMobile) {
    return (
      <Stack spacing={1}>
        {order.marketplaceItems.map((item) => {
          const card = item.marketplaceCard;
          return (
            <Paper key={item.id} variant="outlined" sx={{p: 1.5}}>
              <Stack spacing={1.5}>
                <Box sx={{display: "flex", justifyContent: "center"}}>
                  <CardImage src={card?.primaryImageUrl} name={card?.name ?? ""} size={96} />
                </Box>
                <Stack spacing={0.5} sx={{minWidth: 0}}>
                  <Typography variant="body2" sx={{fontFamily: "monospace"}}>
                    {card?.name}
                  </Typography>
                  <InfoRow
                    label="Артикул"
                    value={card?.offerId ? <CopyableText value={card.offerId} /> : "—"}
                  />
                  <InfoRow
                    label="SKU"
                    value={
                      <Typography sx={{fontFamily: "monospace"}}>{card?.sku ?? "—"}</Typography>
                    }
                  />
                  <InfoRow
                    label="Количество"
                    value={
                      <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
                        <span>{item.quantity}</span>
                        <ReturnedChip item={item} />
                      </Stack>
                    }
                  />
                  <InfoRow
                    label="Позиция каталога"
                    value={
                      card?.catalogItemId ? (
                        <CatalogItemLink
                          catalogItemId={card.catalogItemId}
                          onOpen={openCatalogItem}
                        >
                          <Stack>
                            <Typography variant="body2">{card.catalogItemFullName}</Typography>
                            <Typography variant="caption" color="text.secondary">
                              {card.catalogItemArticle}
                            </Typography>
                          </Stack>
                        </CatalogItemLink>
                      ) : (
                        "—"
                      )
                    }
                  />
                </Stack>
              </Stack>
            </Paper>
          );
        })}
      </Stack>
    );
  }

  return (
    <Box sx={{overflowX: "auto"}}>
      <Table>
        <TableHead>
          <TableRow>
            <TableCell></TableCell>
            <TableCell>Название</TableCell>
            <TableCell>Артикул</TableCell>
            <TableCell>SKU</TableCell>
            <TableCell>Количество</TableCell>
            <TableCell>Позиция каталога</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {order.marketplaceItems.map((item) => (
            <TableRow key={item.id}>
              <TableCell>
                <CardImage
                  src={item.marketplaceCard?.primaryImageUrl}
                  name={item.marketplaceCard?.name ?? ""}
                />
              </TableCell>
              <TableCell sx={{fontFamily: "monospace"}}>{item.marketplaceCard?.name}</TableCell>
              <TableCell>
                {item.marketplaceCard?.offerId && (
                  <CopyableText value={item.marketplaceCard.offerId} />
                )}
              </TableCell>
              <TableCell sx={{fontFamily: "monospace"}}>
                {item.marketplaceCard?.sku ?? "—"}
              </TableCell>
              <TableCell>
                <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
                  <span>{item.quantity}</span>
                  <ReturnedChip item={item} />
                </Stack>
              </TableCell>
              <TableCell>
                {item.marketplaceCard?.catalogItemId ? (
                  <CatalogItemLink
                    catalogItemId={item.marketplaceCard.catalogItemId}
                    onOpen={openCatalogItem}
                  >
                    <Stack>
                      <Typography variant="body2">
                        {item.marketplaceCard.catalogItemFullName}
                      </Typography>
                      <Typography variant="caption" color="text.secondary">
                        {item.marketplaceCard.catalogItemArticle}
                      </Typography>
                    </Stack>
                  </CatalogItemLink>
                ) : (
                  "—"
                )}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </Box>
  );
}

export default OrderMarketplaceItemsSection;
