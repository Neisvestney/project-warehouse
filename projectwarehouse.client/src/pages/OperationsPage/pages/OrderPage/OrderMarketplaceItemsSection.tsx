import type {OrderDetailsDto} from "@/api";
import {Table, TableHead, TableRow, TableCell, TableBody, Stack, Typography} from "@mui/material";
import CardImage from "@/pages/SettingsPage/pages/MarketplacesSettingsPage/components/CardImage.tsx";
import {useOpenCatalogItem} from "@/components/catalog/CatalogItemDrawerContext.ts";
import CatalogItemLink from "@/components/catalog/CatalogItemLink.tsx";

interface OrderBoxesSectionProps {
    order: OrderDetailsDto;
}

function OrderMarketplaceItemsSection({order}: OrderBoxesSectionProps) {
    const openCatalogItem = useOpenCatalogItem();
    
    return <Table>
        <TableHead>
            <TableRow>
                <TableCell></TableCell>
                <TableCell>Название</TableCell>
                <TableCell>Артиукл</TableCell>
                <TableCell>SKU</TableCell>
                <TableCell>Количество</TableCell>
                <TableCell>Позиция каталога</TableCell>
            </TableRow>
        </TableHead>
        <TableBody>
            {order.marketplaceItems.map(item => (
                <TableRow key={item.id}>
                    <TableCell>
                        <CardImage src={item.marketplaceCard?.primaryImageUrl} name={item.marketplaceCard?.name ?? ""} />
                    </TableCell>
                    <TableCell sx={{fontFamily: "monospace"}}>{item.marketplaceCard?.name}</TableCell>
                    <TableCell>{item.marketplaceCard?.offerId}</TableCell>
                    <TableCell sx={{fontFamily: "monospace"}}>{item.marketplaceCard?.sku ?? "—"}</TableCell>
                    <TableCell>{item.quantity}</TableCell>
                    <TableCell>
                        {item.marketplaceCard?.catalogItemId ? (
                            <CatalogItemLink catalogItemId={item.marketplaceCard.catalogItemId} onOpen={openCatalogItem}>
                                <Stack>
                                    <Typography variant="body2">{item.marketplaceCard.catalogItemFullName}</Typography>
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
}

export default OrderMarketplaceItemsSection;