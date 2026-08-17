import {Fragment, useState} from "react";
import {
  Collapse,
  IconButton,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
} from "@mui/material";
import KeyboardArrowDownIcon from "@mui/icons-material/KeyboardArrowDown";
import KeyboardArrowUpIcon from "@mui/icons-material/KeyboardArrowUp";
import {useQuery} from "@tanstack/react-query";
import {marketplacesGetSyncRunsOptions} from "@/api/@tanstack/react-query.gen";
import {usePaginatedParams} from "@/hooks/usePaginatedParams";
import DataTableContainer from "@/components/DataTableContainer";
import TableRowLoader from "@/components/TableRowLoader";
import TableRowEmpty from "@/components/TableRowEmpty";
import MarketplaceStatusChip from "../../components/MarketplaceStatusChip";
import SyncErrorAlert from "../../components/SyncErrorAlert";
import {SYNC_SCOPE_LABELS, formatDateTime, formatDuration} from "../../marketplaceUtils";

const RUNNING_POLL_MS = 3000;

interface AccountSyncRunsTabProps {
  accountId: string;
  isRunning: boolean;
}

function AccountSyncRunsTab({accountId, isRunning}: AccountSyncRunsTabProps) {
  const [expandedId, setExpandedId] = useState<string | null>(null);

  const {fetchParams, page, setPage, pageSize, setPageSize} = usePaginatedParams({}, [], {}, []);

  const {data, isLoading, isFetching} = useQuery({
    ...marketplacesGetSyncRunsOptions({path: {id: accountId}, query: fetchParams}),
    refetchInterval: isRunning ? RUNNING_POLL_MS : false,
  });

  return (
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
            <TableCell />
            <TableCell>Начат</TableCell>
            <TableCell>Длительность</TableCell>
            <TableCell>Объём</TableCell>
            <TableCell>Статус</TableCell>
            <TableCell>Складов</TableCell>
            <TableCell>Карточек</TableCell>
            <TableCell>Автосопоставлено</TableCell>
            <TableCell>Заказов</TableCell>
            <TableCell>Кем запущен</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {isLoading ? (
            <TableRowLoader colSpan={9} />
          ) : data?.items.length === 0 ? (
            <TableRowEmpty colSpan={9} message="Синхронизаций ещё не было" />
          ) : (
            data?.items.map((run) => (
              <Fragment key={run.id}>
                <TableRow hover>
                  <TableCell sx={{width: 48}}>
                    {run.error && (
                      <IconButton
                        size="small"
                        onClick={() => setExpandedId(expandedId === run.id ? null : run.id)}
                      >
                        {expandedId === run.id ? (
                          <KeyboardArrowUpIcon />
                        ) : (
                          <KeyboardArrowDownIcon />
                        )}
                      </IconButton>
                    )}
                  </TableCell>
                  <TableCell>{formatDateTime(run.startedAt)}</TableCell>
                  <TableCell>{formatDuration(run.startedAt, run.finishedAt)}</TableCell>
                  <TableCell>{SYNC_SCOPE_LABELS[run.scope]}</TableCell>
                  <TableCell>
                    <MarketplaceStatusChip status={run.status} />
                  </TableCell>
                  <TableCell>
                    {["warehouse", "all"].includes(run.scope) ? run.warehousesProcessed : "—"}
                  </TableCell>
                  <TableCell sx={{whiteSpace: "pre-wrap"}}>
                    {["cards", "all"].includes(run.scope)
                      ? `${run.cardsProcessed}\n(+${run.cardsCreated} / ~${run.cardsUpdated} / −${run.cardsArchived})`
                      : "—"}
                  </TableCell>
                  <TableCell>
                    {["cards", "all"].includes(run.scope) ? run.autoMapped : "—"}
                  </TableCell>
                  <TableCell sx={{whiteSpace: "pre-wrap"}}>
                    {["orders"].includes(run.scope)
                      ? `${run.ordersProcessed}\n(+${run.ordersCreated}) / ~${run.ordersUpdated} / >${run.ordersSkipped}`
                      : "—"}
                  </TableCell>
                  <TableCell>{run.triggeredByName ?? "Планировщик"}</TableCell>
                </TableRow>
                {run.error && (
                  <TableRow>
                    <TableCell sx={{py: 0, borderBottom: "none"}} colSpan={10}>
                      <Collapse in={expandedId === run.id} unmountOnExit>
                        <SyncErrorAlert error={run.error} title="Запуск завершился ошибкой" />
                      </Collapse>
                    </TableCell>
                  </TableRow>
                )}
              </Fragment>
            ))
          )}
        </TableBody>
      </Table>
    </DataTableContainer>
  );
}

export default AccountSyncRunsTab;
