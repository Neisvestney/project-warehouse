import {
  Button,
  Chip,
  IconButton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
} from "@mui/material";
import {useQuery} from "@tanstack/react-query";
import {Link as RouterLink, useNavigate} from "react-router";
import {inboundOrdersGetAllOptions} from "@/api/@tanstack/react-query.gen";
import {useDebouncedSyncedWithQueryState} from "@/hooks/useDebouncedSyncedWithQueryState";
import {INBOUND_ORDER_STATUS_COLORS, INBOUND_ORDER_STATUS_LABELS} from "@/utils/inboundOrderUtils";
import {usePaginatedParams} from "@/hooks/usePaginatedParams";
import {useHasPermission} from "@/hooks/usePermission";
import PageGenericHeader from "@/components/PageGenericHeader";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import RefreshIcon from "@mui/icons-material/Refresh";
import AddIcon from "@mui/icons-material/Add";
import SearchInput from "@/components/SearchInput";
import DataTableContainer from "@/components/DataTableContainer";
import TableRowLoader from "@/components/TableRowLoader";
import TableRowEmpty from "@/components/TableRowEmpty";

function InboundOrdersPage() {
  const navigate = useNavigate();
  const canCreate = useHasPermission([
    "inbound_orders.edit",
    "inbound_orders.edit_assigned_warehouses",
  ]);

  const [inputValue, setInputValue, searchString] = useDebouncedSyncedWithQueryState(
    "search",
    (q) => (typeof q === "string" ? q : ""),
    (v) => v || null,
  );

  const {fetchParams, page, setPage, pageSize, setPageSize} = usePaginatedParams(
    {},
    [],
    {searchString},
    [searchString],
  );

  const {data, isLoading, isFetching, refetch} = useQuery(
    inboundOrdersGetAllOptions({query: fetchParams}),
  );

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs
        path={[{name: "Приходные ордера", link: "/inbound-orders"}, {name: "Список"}]}
      />
      <PageGenericHeader
        title="Приходные ордера"
        right={
          <>
            <IconButton color="inherit" onClick={() => refetch()}>
              <RefreshIcon />
            </IconButton>
            {canCreate && (
              <Button
                variant="outlined"
                endIcon={<AddIcon />}
                component={RouterLink}
                to="/inbound-orders/new"
              >
                Создать
              </Button>
            )}
          </>
        }
      >
        <SearchInput value={inputValue} onChange={setInputValue} />
      </PageGenericHeader>
      <DataTableContainer
        isFetching={isFetching}
        count={data?.total ?? 0}
        page={page}
        onPageChange={setPage}
        rowsPerPage={pageSize}
        onRowsPerPageChange={setPageSize}
      >
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>№</TableCell>
              <TableCell>Название</TableCell>
              <TableCell>Статус</TableCell>
              <TableCell>Склад</TableCell>
              <TableCell>Дата начала</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {isLoading ? (
              <TableRowLoader colSpan={5} />
            ) : data?.items.length === 0 ? (
              <TableRowEmpty colSpan={5} message="Ордера не найдены" />
            ) : (
              data?.items.map((order) => (
                <TableRow
                  key={order.id}
                  hover
                  sx={{
                    cursor: "pointer",
                    opacity: isFetching && !isLoading ? 0.5 : 1,
                    transition: "opacity 0.2s",
                  }}
                  onClick={() => navigate(`/inbound-orders/${order.id}`)}
                >
                  <TableCell>#{order.number}</TableCell>
                  <TableCell>{order.title ?? "—"}</TableCell>
                  <TableCell>
                    <Chip
                      label={INBOUND_ORDER_STATUS_LABELS[order.status]}
                      color={INBOUND_ORDER_STATUS_COLORS[order.status]}
                      size="small"
                    />
                  </TableCell>
                  <TableCell>{order.warehouse.name}</TableCell>
                  <TableCell>
                    {new Date(order.plannedStartDateTime).toLocaleString("ru-RU", {
                      day: "2-digit",
                      month: "2-digit",
                      year: "numeric",
                      hour: "2-digit",
                      minute: "2-digit",
                    })}
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </DataTableContainer>
    </Stack>
  );
}

export default InboundOrdersPage;
