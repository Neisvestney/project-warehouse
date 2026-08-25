import {useMemo} from "react";
import {Alert, IconButton, Stack, Tooltip} from "@mui/material";
import RefreshIcon from "@mui/icons-material/Refresh";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import PageGenericHeader from "@/components/PageGenericHeader";
import QueryError from "@/components/QueryError";
import StockMovementsFilters from "./StockMovementsFilters";
import StockMovementsPivotTable from "./StockMovementsPivotTable";
import {useCatalogItemsByIds} from "./useCatalogItemsByIds";
import {useStockMovementsFilters} from "./useStockMovementsFilters";
import {useStockMovementsPivot} from "./useStockMovementsPivot";

function StockMovementsPage() {
  const filters = useStockMovementsFilters();
  const {filter, showTransfers} = filters;

  const knownItems = useCatalogItemsByIds(filter.catalogItemIds);
  const items = useMemo(
    () =>
      filter.catalogItemIds.map(
        (id) =>
          knownItems.get(id) ?? {
            id,
            type: "standard" as const,
            name: "…",
            fullName: "…",
            article: "",
            isArchived: false,
          },
      ),
    [filter.catalogItemIds, knownItems],
  );

  const pivot = useStockMovementsPivot(filter);
  const hasSelection = filter.catalogItemIds.length > 0;

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs path={[{name: "Движения товаров"}]} />

      <PageGenericHeader
        title={
          // The applied zone hangs off a permanently rendered icon: as a line of its own it appeared
          // and vanished with every reload and shifted the page under the cursor.
          <>
            Движения товаров
            <Tooltip
              title={pivot.timeZoneId && `Сутки считаются по часовому поясу ${pivot.timeZoneId}`}
            >
              <InfoOutlinedIcon
                sx={{
                  fontSize: "0.7em",
                  verticalAlign: "middle",
                  ml: 0.5,
                  color: "primary.main",
                  opacity: pivot.timeZoneId ? 1 : 0,
                  pointerEvents: pivot.timeZoneId ? "auto" : "none",
                }}
              />
            </Tooltip>
          </>
        }
        refresh={
          <Tooltip title="Обновить">
            <IconButton color="inherit" onClick={() => pivot.refetch()}>
              <RefreshIcon />
            </IconButton>
          </Tooltip>
        }
      />

      <StockMovementsFilters {...filters} items={items} />

      {!hasSelection ? (
        <Alert severity="info">
          Выберите хотя бы одну позицию каталога — из них строятся столбцы таблицы.
        </Alert>
      ) : pivot.error ? (
        <QueryError />
      ) : (
        <>
          <StockMovementsPivotTable
            columns={items}
            rows={pivot.rows}
            showTransfers={showTransfers}
            isLoading={pivot.isLoading}
            isFetching={pivot.isFetching}
            isFetchingNextPage={pivot.isFetchingNextPage}
            hasNextPage={pivot.hasNextPage}
            onLoadMore={() => pivot.fetchNextPage()}
          />
        </>
      )}
    </Stack>
  );
}

export default StockMovementsPage;
